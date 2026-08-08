using System.Globalization;
using System.Text;

namespace D2ST.Api.Economy;

/// <summary>
/// A sellable definition discovered in the local Dota client schema. The
/// schema tells us what the client can render; it does not define the price in
/// the server's local USD economy.
/// </summary>
public sealed record DotaCatalogDefinition(
    uint DefIndex,
    string Name,
    string DisplayName,
    string ItemName,
    string Description,
    string Prefab,
    string Slot,
    string Quality,
    string Rarity,
    string ImageInventory,
    IReadOnlyList<string> HeroNames);

public sealed record DotaCatalogSource(
    string DotaPath,
    string PakPath,
    string? SteamInfPath,
    uint ClientVersion,
    int ParsedDefinitionCount,
    IReadOnlyList<DotaCatalogDefinition> Items);

/// <summary>
/// Reads the item schema from a Dota installation. The importer intentionally
/// keeps prices out of this class: item_cost in items_game.txt is gameplay gold
/// and must never silently become a store price.
/// </summary>
public sealed class DotaCatalogImporter
{
    private const uint VpkSignature = 0x55AA1234;
    private const int MaxSchemaBytes = 128 * 1024 * 1024;
    private const string ItemsSchemaPath = "scripts/items/items_game.txt";
    private const string EnglishItemsLocalizationPath = "resource/localization/items_english.txt";

    private static readonly HashSet<string> BlockedPrefabs = new(StringComparer.OrdinalIgnoreCase)
    {
        "autograph_rune",
        "battle_pass",
        "compendium",
        "dynamic_recipe",
        "emoticon_tool",
        "event_game",
        "fantasy_ticket",
        "gift",
        "key",
        "league",
        "misc_tool",
        "pennant",
        "player_card",
        "recipe",
        "retired_treasure_chest",
        "socket_gem",
        "sticker",
        "sticker_capsule",
        "tool",
        "treasure_chest"
    };

    private static readonly HashSet<string> BlockedSlots = new(StringComparer.OrdinalIgnoreCase)
    {
        "tool",
        "socket_gem"
    };

    private static readonly HashSet<string> GlobalSlots = new(StringComparer.OrdinalIgnoreCase)
    {
        "ancient",
        "heroic_statue",
        "multikill_banner",
        "pet_effigy",
        "summons",
        "announcer",
        "courier",
        "courier_effect",
        "cursor_pack",
        "death_effect",
        "direcreeps",
        "diresiegecreeps",
        "diretowers",
        "emblem",
        "head_effect",
        "hud_skin",
        "loading_screen",
        "map_effect",
        "music",
        "radiantcreeps",
        "radiantsiegecreeps",
        "radianttowers",
        "roshan",
        "streak_effect",
        "terrain",
        "tormentor",
        "versus_screen",
        "ward",
        "weather",
        "teleport_effect",
        "blink_effect"
    };

    private static readonly Dictionary<string, string> GlobalPrefabSlots = new(StringComparer.OrdinalIgnoreCase)
    {
        ["announcer"] = "announcer",
        ["courier"] = "courier",
        ["courier_wearable"] = "courier",
        ["courier_effect"] = "courier_effect",
        ["cursor_pack"] = "cursor_pack",
        ["death_effect"] = "death_effect",
        ["direcreeps"] = "direcreeps",
        ["diresiegecreeps"] = "diresiegecreeps",
        ["diretowers"] = "diretowers",
        ["emblem"] = "emblem",
        ["head_effect"] = "head_effect",
        ["hud_skin"] = "hud_skin",
        ["loading_screen"] = "loading_screen",
        ["map_effect"] = "map_effect",
        ["music"] = "music",
        ["radiantcreeps"] = "radiantcreeps",
        ["radiantsiegecreeps"] = "radiantsiegecreeps",
        ["radianttowers"] = "radianttowers",
        ["roshan"] = "roshan",
        ["streak_effect"] = "streak_effect",
        ["terrain"] = "terrain",
        ["tormentor"] = "tormentor",
        ["versus_screen"] = "versus_screen",
        ["ward"] = "ward",
        ["teleport_effect"] = "teleport_effect",
        ["blink_effect"] = "blink_effect"
    };

    public DotaCatalogSource Read(string requestedPath)
    {
        var (dotaPath, pakPath) = ResolvePaths(requestedPath);
        var schema = VpkTextReader.ReadText(pakPath, ItemsSchemaPath);
        var localization = ReadEnglishItemLocalization(pakPath);
        var parsed = ParseItems(schema, localization);
        var version = ReadClientVersion(dotaPath);

        return new DotaCatalogSource(
            dotaPath,
            pakPath,
            version.SteamInfPath,
            version.ClientVersion,
            parsed.ParsedDefinitionCount,
            parsed.Items);
    }

    private static (string DotaPath, string PakPath) ResolvePaths(string requestedPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            throw new InvalidDataException("Debes indicar la ruta raíz de Dota 2.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(requestedPath.Trim().Trim('"'));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidDataException("La ruta de Dota 2 no es válida.", exception);
        }

        if (File.Exists(fullPath) &&
            string.Equals(Path.GetFileName(fullPath), "pak01_dir.vpk", StringComparison.OrdinalIgnoreCase))
        {
            return (Path.GetDirectoryName(fullPath) ?? fullPath, fullPath);
        }

        var candidates = new[]
        {
            Path.Combine(fullPath, "game", "dota", "pak01_dir.vpk"),
            Path.Combine(fullPath, "dota", "pak01_dir.vpk"),
            Path.Combine(fullPath, "pak01_dir.vpk")
        };
        var pakPath = candidates.FirstOrDefault(File.Exists);
        if (pakPath is null)
        {
            throw new FileNotFoundException(
                "No se encontró pak01_dir.vpk. Selecciona la raíz de Dota 2, game\\dota o el archivo VPK.",
                candidates[0]);
        }

        return (fullPath, pakPath);
    }

    private static (uint ClientVersion, string? SteamInfPath) ReadClientVersion(string dotaPath)
    {
        var candidates = new[]
        {
            Path.Combine(dotaPath, "game", "dota", "steam.inf"),
            Path.Combine(dotaPath, "dota", "steam.inf"),
            Path.Combine(dotaPath, "steam.inf")
        };
        var steamInfPath = candidates.FirstOrDefault(File.Exists);
        if (steamInfPath is null)
        {
            return (0, null);
        }

        try
        {
            foreach (var line in File.ReadLines(steamInfPath))
            {
                var entry = line.Trim();
                if (entry.Length == 0 || entry.StartsWith('#') || entry.StartsWith(';') ||
                    entry.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                var separator = entry.IndexOf('=');
                if (separator <= 0 ||
                    !string.Equals(entry[..separator].Trim(), "ClientVersion", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return uint.TryParse(
                    entry[(separator + 1)..].Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var version)
                    ? (version, steamInfPath)
                    : (0, steamInfPath);
            }
        }
        catch (IOException)
        {
            // The schema is still useful when steam.inf is temporarily locked.
        }
        catch (UnauthorizedAccessException)
        {
            // The schema is still useful when steam.inf is not readable.
        }

        return (0, steamInfPath);
    }

    private static (int ParsedDefinitionCount, IReadOnlyList<DotaCatalogDefinition> Items) ParseItems(
        string text,
        IReadOnlyDictionary<string, string> localization)
    {
        var root = ValveKeyValues.Parse(text);
        var itemsSection = root.Child("items")
            ?? root.Child("items_game")?.Child("items")
            ?? root.Descendant("items")
            ?? throw new InvalidDataException("items_game.txt no contiene la sección items.");
        var items = new List<DotaCatalogDefinition>();
        var parsedCount = 0;

        foreach (var entry in itemsSection.Children)
        {
            if (!uint.TryParse(entry.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var defIndex) ||
                defIndex == 0 || entry.Children.Count == 0)
            {
                continue;
            }

            parsedCount++;
            var definition = ToDefinition(defIndex, entry, localization);
            if (IsSellableCandidate(definition))
            {
                items.Add(definition);
            }
        }

        return (parsedCount, items.OrderBy(item => item.DefIndex).ToArray());
    }

    private static DotaCatalogDefinition ToDefinition(
        uint defIndex,
        ValveKeyValueNode entry,
        IReadOnlyDictionary<string, string> localization)
    {
        var name = FirstNonEmpty(entry.ValueOf("name"), entry.ValueOf("item_name"), $"def_{defIndex}");
        var itemName = entry.ValueOf("item_name");
        var displayName = ResolveDisplayName(name, itemName, localization);
        var description = entry.ValueOf("item_description");
        var prefab = Normalize(entry.ValueOf("prefab"));
        var slot = Normalize(FirstNonEmpty(entry.ValueOf("item_slot"), entry.ValueOf("loadout_slot")));
        var heroes = entry.Child("used_by_heroes")?.Children
            .Where(child => child.Key.StartsWith("npc_dota_hero_", StringComparison.OrdinalIgnoreCase))
            .Where(child => string.IsNullOrWhiteSpace(child.Value) || child.Value == "1")
            .Select(child => child.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(hero => hero, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (GlobalPrefabSlots.TryGetValue(prefab, out var globalSlot) &&
            !GlobalSlots.Contains(slot))
        {
            slot = globalSlot;
        }

        return new DotaCatalogDefinition(
            defIndex,
            name,
            displayName,
            itemName,
            description,
            prefab,
            slot,
            entry.ValueOf("item_quality"),
            entry.ValueOf("item_rarity"),
            entry.ValueOf("image_inventory"),
            heroes);
    }

    private static IReadOnlyDictionary<string, string> ReadEnglishItemLocalization(string pakPath)
    {
        try
        {
            var text = VpkTextReader.ReadText(pakPath, EnglishItemsLocalizationPath);
            var tokens = ValveKeyValues.Parse(text).Descendant("Tokens");
            if (tokens is null)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            return tokens.Children
                .Where(child => child.Value is not null && !string.IsNullOrWhiteSpace(child.Key))
                .GroupBy(child => child.Key.TrimStart('#'), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().Value!.Trim(),
                    StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is FileNotFoundException
            or InvalidDataException
            or IOException
            or UnauthorizedAccessException)
        {
            // Some stripped client builds omit localization. The raw schema
            // remains importable and the administrator can set a market name
            // manually in that case.
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string ResolveDisplayName(
        string name,
        string itemName,
        IReadOnlyDictionary<string, string> localization)
    {
        var token = itemName.Trim().TrimStart('#');
        if (token.Length > 0 && localization.TryGetValue(token, out var localized))
        {
            return localized;
        }

        return name.Trim();
    }

    private static bool IsSellableCandidate(DotaCatalogDefinition item)
    {
        if (item.Name.Contains("default", StringComparison.OrdinalIgnoreCase) ||
            item.Prefab.Contains("default_item", StringComparison.OrdinalIgnoreCase) ||
            BlockedPrefabs.Contains(item.Prefab) ||
            BlockedSlots.Contains(item.Slot))
        {
            return false;
        }

        var global = GlobalPrefabSlots.ContainsKey(item.Prefab) || GlobalSlots.Contains(item.Slot);
        var heroCosmetic = item.HeroNames.Count > 0 && !string.IsNullOrWhiteSpace(item.Slot);
        return global || heroCosmetic;
    }

    private static string Normalize(string value) =>
        value.Trim().ToLowerInvariant().Replace(' ', '_');

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static class VpkTextReader
    {
        public static string ReadText(string dirPath, string wantedPath)
        {
            var wanted = wantedPath.Replace('\\', '/').TrimStart('/').ToLowerInvariant();
            using var stream = File.OpenRead(dirPath);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
            if (reader.ReadUInt32() != VpkSignature)
            {
                throw new InvalidDataException("El archivo no tiene una firma VPK válida.");
            }

            var version = reader.ReadUInt32();
            var treeLength = reader.ReadUInt32();
            if (version == 2)
            {
                _ = reader.ReadUInt32();
                _ = reader.ReadUInt32();
                _ = reader.ReadUInt32();
                _ = reader.ReadUInt32();
            }

            var treeStart = stream.Position;
            var treeEnd = checked(treeStart + treeLength);
            if (treeEnd > stream.Length)
            {
                throw new InvalidDataException("El árbol del VPK excede el tamaño del archivo.");
            }

            while (stream.Position < treeEnd)
            {
                var extension = ReadNullString(reader);
                if (extension.Length == 0)
                {
                    break;
                }

                while (true)
                {
                    var directory = ReadNullString(reader);
                    if (directory.Length == 0)
                    {
                        break;
                    }

                    while (true)
                    {
                        var fileName = ReadNullString(reader);
                        if (fileName.Length == 0)
                        {
                            break;
                        }

                        _ = reader.ReadUInt32();
                        var preloadLength = reader.ReadUInt16();
                        var archiveIndex = reader.ReadUInt16();
                        var entryOffset = reader.ReadUInt32();
                        var entryLength = reader.ReadUInt32();
                        _ = reader.ReadUInt16();
                        var preload = preloadLength == 0 ? [] : reader.ReadBytes(preloadLength);
                        var fullPath = BuildPath(directory, fileName, extension);
                        if (!string.Equals(fullPath, wanted, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (entryLength > MaxSchemaBytes || entryLength > int.MaxValue - preload.Length)
                        {
                            throw new InvalidDataException($"{wantedPath} es demasiado grande para importarlo.");
                        }

                        var payload = new byte[preload.Length + (int)entryLength];
                        Buffer.BlockCopy(preload, 0, payload, 0, preload.Length);
                        if (entryLength > 0)
                        {
                            var archivePath = archiveIndex == 0x7FFF
                                ? dirPath
                                : Path.Combine(
                                    Path.GetDirectoryName(dirPath)!,
                                    $"{Path.GetFileNameWithoutExtension(dirPath).Replace("_dir", string.Empty)}_{archiveIndex:D3}.vpk");
                            using var archive = File.OpenRead(archivePath);
                            if ((ulong)entryOffset + entryLength > (ulong)archive.Length)
                            {
                                throw new InvalidDataException("Una entrada del VPK excede el archivo de datos.");
                            }

                            archive.Position = entryOffset;
                            archive.ReadExactly(payload, preload.Length, (int)entryLength);
                        }

                        return Encoding.UTF8.GetString(payload);
                    }
                }
            }

            throw new FileNotFoundException($"No se encontró {wantedPath} dentro del VPK.", wantedPath);
        }

        private static string BuildPath(string directory, string fileName, string extension)
        {
            var name = extension == " " ? fileName : $"{fileName}.{extension}";
            return (directory == " " ? name : $"{directory}/{name}").ToLowerInvariant();
        }

        private static string ReadNullString(BinaryReader reader)
        {
            var bytes = new List<byte>(64);
            while (true)
            {
                var value = reader.ReadByte();
                if (value == 0)
                {
                    return Encoding.UTF8.GetString(bytes.ToArray());
                }

                bytes.Add(value);
            }
        }
    }

    private sealed class ValveKeyValueNode
    {
        public string Key { get; }
        public string? Value { get; }
        public List<ValveKeyValueNode> Children { get; } = [];

        public ValveKeyValueNode(string key, string? value)
        {
            Key = key;
            Value = value;
        }

        public ValveKeyValueNode? Child(string key) =>
            Children.FirstOrDefault(child => string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase));

        public ValveKeyValueNode? Descendant(string key)
        {
            foreach (var child in Children)
            {
                if (string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }

                var descendant = child.Descendant(key);
                if (descendant is not null)
                {
                    return descendant;
                }
            }

            return null;
        }

        public string ValueOf(string key) => Child(key)?.Value ?? string.Empty;
    }

    private static class ValveKeyValues
    {
        public static ValveKeyValueNode Parse(string text)
        {
            var tokenizer = new Tokenizer(text);
            var root = new ValveKeyValueNode(string.Empty, null);
            ParseChildren(root, tokenizer, stopAtBrace: false);
            return root;
        }

        private static void ParseChildren(ValveKeyValueNode parent, Tokenizer tokenizer, bool stopAtBrace)
        {
            while (tokenizer.Next() is { } key)
            {
                if (key == "}")
                {
                    if (stopAtBrace)
                    {
                        return;
                    }

                    throw new InvalidDataException("KeyValues contiene una llave de cierre inesperada.");
                }

                var value = tokenizer.Next()
                    ?? throw new InvalidDataException($"Falta el valor de KeyValues para '{key}'.");
                if (value == "{")
                {
                    var child = new ValveKeyValueNode(key, null);
                    ParseChildren(child, tokenizer, stopAtBrace: true);
                    parent.Children.Add(child);
                    continue;
                }

                if (value == "}")
                {
                    throw new InvalidDataException($"Falta el valor de KeyValues para '{key}'.");
                }

                parent.Children.Add(new ValveKeyValueNode(key, value));
            }

            if (stopAtBrace)
            {
                throw new InvalidDataException("KeyValues contiene un bloque sin cerrar.");
            }
        }

        private sealed class Tokenizer
        {
            private readonly string _text;
            private int _position;

            public Tokenizer(string text) => _text = text;

            public string? Next()
            {
                SkipTrivia();
                if (_position >= _text.Length)
                {
                    return null;
                }

                var current = _text[_position];
                if (current is '{' or '}')
                {
                    _position++;
                    return current.ToString();
                }

                if (current == '"')
                {
                    return ReadQuoted();
                }

                var start = _position;
                while (_position < _text.Length &&
                       !char.IsWhiteSpace(_text[_position]) &&
                       _text[_position] is not ('{' or '}'))
                {
                    _position++;
                }

                return _text[start.._position];
            }

            private string ReadQuoted()
            {
                _position++;
                var builder = new StringBuilder();
                while (_position < _text.Length)
                {
                    var current = _text[_position++];
                    if (current == '"')
                    {
                        return builder.ToString();
                    }

                    if (current == '\\' && _position < _text.Length)
                    {
                        builder.Append(_text[_position++]);
                    }
                    else
                    {
                        builder.Append(current);
                    }
                }

                throw new InvalidDataException("KeyValues contiene una cadena sin cerrar.");
            }

            private void SkipTrivia()
            {
                while (_position < _text.Length)
                {
                    if (char.IsWhiteSpace(_text[_position]))
                    {
                        _position++;
                        continue;
                    }

                    if (_text[_position] == '/' && _position + 1 < _text.Length && _text[_position + 1] == '/')
                    {
                        _position += 2;
                        while (_position < _text.Length && _text[_position] is not ('\r' or '\n'))
                        {
                            _position++;
                        }

                        continue;
                    }

                    break;
                }
            }
        }
    }
}
