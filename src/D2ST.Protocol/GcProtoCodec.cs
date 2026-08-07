using ProtoBuf;

namespace D2ST.Protocol;

/// <summary>protobuf-net (de)serialization for GC message bodies.</summary>
public interface IGcProtoCodec
{
    byte[] Encode<T>(T message);
    T Decode<T>(byte[] payload);
}

public sealed class GcProtoCodec : IGcProtoCodec
{
    public byte[] Encode<T>(T message)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        return stream.ToArray();
    }

    public T Decode<T>(byte[] payload)
    {
        using var stream = new MemoryStream(payload);
        return Serializer.Deserialize<T>(stream);
    }
}
