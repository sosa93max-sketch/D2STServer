<#
.SYNOPSIS
    Script avanzado de restauración de paquetes NuGet con reintentos y diagnóstico detallado
.DESCRIPTION
    Este script ejecuta dotnet restore con reintentos, mostrando información detallada
    sobre el progreso, estado de la caché, tiempos de respuesta y diagnóstico de red.
    Incluye correcciones para compatibilidad entre PowerShell 5.1 y 7+
.NOTES
    Version: 1.1
    Autor: Maxwell
    Fecha: 2026-08-01
#>

# ==================== CONFIGURACION ====================
$maxAttempts = 20
$waitSeconds = 20
$solutionFile = "D2STServer.sln"
$nugetCachePath = "$env:USERPROFILE\.nuget\packages"
$logFile = "restore-log-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"

# ==================== FUNCIONES DE DIAGNOSTICO ====================

function Write-DetailedLog {
    param(
        [string]$Message,
        [string]$Color = "White",
        [string]$LogLevel = "INFO"
    )
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$LogLevel] $Message"
    
    # Escribir en consola con color
    Write-Host $logMessage -ForegroundColor $Color
    
    # Escribir en archivo de log
    Add-Content -Path $logFile -Value $logMessage
}

function Test-NetworkConnectivity {
    param([string]$Url)
    
    try {
        $hostName = (New-Object Uri($Url)).Host
        
        # Compatibilidad entre PowerShell 5.1 y 7+
        if ($PSVersionTable.PSVersion.Major -ge 6) {
            $pingResult = Test-Connection -TargetName $hostName -Count 2 -Quiet -ErrorAction SilentlyContinue
        } else {
            $pingResult = Test-Connection -ComputerName $hostName -Count 2 -Quiet -ErrorAction SilentlyContinue
        }
        
        if ($pingResult) {
            return @{ Status = "OK"; Message = "Conectividad OK" }
        } else {
            return @{ Status = "ERROR"; Message = "Sin respuesta de ping" }
        }
    } catch {
        return @{ Status = "ERROR"; Message = $_.Exception.Message }
    }
}

function Get-NuGetCacheInfo {
    $cacheInfo = @{
        TotalSize = 0
        PackageCount = 0
        LargestPackages = @()
        LastModified = $null
        Error = $null
    }
    
    if (Test-Path $nugetCachePath) {
        try {
            # Obtener todos los archivos una sola vez con -File
            $files = Get-ChildItem -Path $nugetCachePath -Recurse -File -ErrorAction SilentlyContinue
            $cacheInfo.PackageCount = $files.Count
            
            # Calcular tamano total (solo archivos .nupkg para mayor precision)
            $nupkgFiles = $files | Where-Object { $_.Extension -eq ".nupkg" }
            $cacheInfo.TotalSize = ($nupkgFiles | Measure-Object -Property Length -Sum).Sum
            
            # Ultima modificacion (reusando $files que ya tiene -File)
            if ($files) {
                $cacheInfo.LastModified = ($files | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
            }
            
            # Top 5 paquetes mas grandes
            $cacheInfo.LargestPackages = $nupkgFiles | 
                                        Sort-Object Length -Descending | 
                                        Select-Object -First 5 | 
                                        ForEach-Object { 
                                            [PSCustomObject]@{
                                                Name = $_.Name
                                                Size = [math]::Round($_.Length / 1MB, 2)
                                            }
                                        }
        } catch {
            $cacheInfo.Error = $_.Exception.Message
        }
    } else {
        $cacheInfo.Error = "Cache no encontrada"
    }
    
    return $cacheInfo
}

function Test-NuGetSources {
    Write-DetailedLog "Verificando fuentes NuGet..." -Color "Cyan" -LogLevel "DIAG"
    
    # Capturar salida de dotnet nuget list source
    $sourcesOutput = dotnet nuget list source 2>&1 | Out-String
    Write-DetailedLog "Fuentes configuradas:" -Color "Yellow" -LogLevel "DIAG"
    Write-DetailedLog $sourcesOutput -Color "Gray" -LogLevel "DIAG"
    
    # Extraer URLs de las fuentes
    $sourceUrls = $sourcesOutput -split "`n" | 
                  Select-String -Pattern "https?://[^\s]+" | 
                  ForEach-Object { $_.Matches[0].Value }
    
    $results = @()
    foreach ($url in $sourceUrls) {
        $test = Test-NetworkConnectivity -Url $url
        $results += [PSCustomObject]@{
            Source = $url
            Status = $test.Status
            Message = $test.Message
        }
    }
    
    return $results
}

function Get-DotNetVersion {
    try {
        $version = dotnet --version 2>&1
        $info = dotnet --info 2>&1 | Out-String
        return @{
            Version = $version
            Info = $info
        }
    } catch {
        return @{
            Version = "ERROR"
            Info = "No se pudo obtener informacion de .NET SDK: $($_.Exception.Message)"
        }
    }
}

function Format-TableAsString {
    param(
        [array]$Data,
        [array]$Columns
    )
    
    # Usar Out-String para convertir la tabla a texto plano
    $tableString = $Data | Format-Table -AutoSize $Columns | Out-String
    
    # Dividir en lineas y filtrar lineas vacias
    $lines = $tableString -split "`r?`n" | Where-Object { $_.Trim() -ne "" }
    
    return $lines
}

# ==================== INICIO DEL SCRIPT ====================

Clear-Host
Write-Host @"
============================================================
     SCRIPT DE RESTAURACION NUGET - DIAGNOSTICO AVANZADO
                          VERSION 1.1
					   Powered By Maxwell
============================================================
"@ -ForegroundColor Cyan

Write-DetailedLog "INICIANDO PROCESO DE RESTAURACION" -Color "Green" -LogLevel "START"
Write-DetailedLog "Solucion: $solutionFile" -Color "White" -LogLevel "CONFIG"
Write-DetailedLog "Intentos maximos: $maxAttempts" -Color "White" -LogLevel "CONFIG"
Write-DetailedLog "Espera entre intentos: $waitSeconds segundos" -Color "White" -LogLevel "CONFIG"
Write-DetailedLog "Log: $logFile" -Color "White" -LogLevel "CONFIG"
Write-DetailedLog "PowerShell Version: $($PSVersionTable.PSVersion)" -Color "White" -LogLevel "CONFIG"

# ==================== DIAGNOSTICO INICIAL ====================

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-DetailedLog "DIAGNOSTICO INICIAL DEL SISTEMA" -Color "Magenta" -LogLevel "DIAG"
Write-Host "============================================================" -ForegroundColor Cyan

# 1. Version de .NET
Write-DetailedLog "VERSION DE .NET SDK" -Color "Yellow" -LogLevel "DIAG"
$dotnetInfo = Get-DotNetVersion
Write-DetailedLog "Version: $($dotnetInfo.Version)" -Color "White" -LogLevel "DIAG"
Write-DetailedLog "Informacion detallada:" -Color "Gray" -LogLevel "DIAG"
$dotnetInfo.Info -split "`r?`n" | ForEach-Object {
    if ($_.Trim()) {
        Write-DetailedLog "  $_" -Color "Gray" -LogLevel "DIAG"
    }
}

# 2. Estado de la cache NuGet
Write-DetailedLog "ESTADO DE LA CACHE NUGET" -Color "Yellow" -LogLevel "DIAG"
$cacheInfo = Get-NuGetCacheInfo

if ($cacheInfo.Error) {
    Write-DetailedLog "Error al leer cache: $($cacheInfo.Error)" -Color "Red" -LogLevel "ERROR"
} else {
    $totalSizeMB = [math]::Round($cacheInfo.TotalSize / 1MB, 2)
    Write-DetailedLog "Paquetes en cache: $($cacheInfo.PackageCount)" -Color "White" -LogLevel "DIAG"
    Write-DetailedLog "Tamano total: $totalSizeMB MB" -Color "White" -LogLevel "DIAG"
    if ($cacheInfo.LastModified) {
        Write-DetailedLog "Ultima modificacion: $($cacheInfo.LastModified)" -Color "White" -LogLevel "DIAG"
    }
    
    if ($cacheInfo.LargestPackages.Count -gt 0) {
        Write-DetailedLog "Top 5 paquetes mas grandes:" -Color "Yellow" -LogLevel "DIAG"
        $cacheInfo.LargestPackages | ForEach-Object {
            Write-DetailedLog "   $($_.Name) - $($_.Size) MB" -Color "Gray" -LogLevel "DIAG"
        }
    }
}

# 3. Verificacion de fuentes NuGet
Write-DetailedLog "VERIFICANDO FUENTES NUGET" -Color "Yellow" -LogLevel "DIAG"
$sourceResults = Test-NuGetSources

if ($sourceResults.Count -eq 0) {
    Write-DetailedLog "No se encontraron fuentes NuGet configuradas" -Color "Yellow" -LogLevel "WARN"
} else {
    foreach ($source in $sourceResults) {
        $color = if ($source.Status -eq "OK") { "Green" } else { "Red" }
        $icon = if ($source.Status -eq "OK") { "[OK]" } else { "[FAIL]" }
        Write-DetailedLog "$icon $($source.Source)" -Color $color -LogLevel "DIAG"
        Write-DetailedLog "   Estado: $($source.Status) - $($source.Message)" -Color "Gray" -LogLevel "DIAG"
    }
}

# ==================== PROCESO DE RESTAURACION ====================

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-DetailedLog "INICIANDO RESTAURACION CON REINTENTOS" -Color "Magenta" -LogLevel "PROCESS"
Write-Host "============================================================" -ForegroundColor Cyan

$attemptCount = 0
$success = $false
$attemptHistory = @()

for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
    $attemptCount++
    $startTime = Get-Date
    
    Write-Host "`n------------------------------------------------------------" -ForegroundColor Yellow
    Write-DetailedLog "INTENTO $attempt DE $maxAttempts" -Color "Yellow" -LogLevel "ATTEMPT"
    Write-Host "------------------------------------------------------------" -ForegroundColor Yellow
    
    # Mostrar estado actual de la cache (solo cada 5 intentos)
    if ($attempt % 5 -eq 0 -or $attempt -eq 1) {
        $currentCache = Get-NuGetCacheInfo
        if (-not $currentCache.Error) {
            $currentSizeMB = [math]::Round($currentCache.TotalSize / 1MB, 2)
            Write-DetailedLog "Tamano de cache actual: $currentSizeMB MB ($($currentCache.PackageCount) paquetes)" -Color "Cyan" -LogLevel "STATUS"
        }
    }
    
    # Ejecutar restore con medicion de tiempo
    Write-DetailedLog "Ejecutando: dotnet restore $solutionFile --disable-parallel --ignore-failed-sources" -Color "White" -LogLevel "CMD"
    
    $restoreStart = Get-Date
    
    # Ejecutar dotnet restore y capturar salida correctamente
    $restoreOutput = dotnet restore $solutionFile --disable-parallel --ignore-failed-sources 2>&1
    # $LASTEXITCODE se preserva correctamente aqui
    
    $restoreEnd = Get-Date
    $restoreDuration = ($restoreEnd - $restoreStart).TotalSeconds
    
    # Mostrar salida completa del comando
    Write-DetailedLog "SALIDA DEL COMANDO:" -Color "Gray" -LogLevel "OUTPUT"
    $restoreOutput | ForEach-Object {
        Write-DetailedLog "  $_" -Color "Gray" -LogLevel "OUTPUT"
    }
    
    # Registrar el intento
    $attemptData = [PSCustomObject]@{
        Attempt = $attempt
        StartTime = $startTime
        Duration = $restoreDuration
        ExitCode = $LASTEXITCODE
        Success = $LASTEXITCODE -eq 0
    }
    $attemptHistory += $attemptData
    
    # Evaluar resultado
    if ($LASTEXITCODE -eq 0) {
        $success = $true
        Write-Host "`n============================================================" -ForegroundColor Green
        Write-DetailedLog "RESTAURACION COMPLETADA CON EXITO!" -Color "Green" -LogLevel "SUCCESS"
        Write-Host "============================================================" -ForegroundColor Green
        Write-DetailedLog "Tiempo de ejecucion: $([math]::Round($restoreDuration, 2)) segundos" -Color "Cyan" -LogLevel "SUCCESS"
        Write-DetailedLog "Intentos utilizados: $attempt" -Color "Cyan" -LogLevel "SUCCESS"
        break
    } else {
        Write-DetailedLog "RESTAURACION FALLIDA (Codigo: $LASTEXITCODE)" -Color "Red" -LogLevel "ERROR"
        Write-DetailedLog "Tiempo de ejecucion: $([math]::Round($restoreDuration, 2)) segundos" -Color "Red" -LogLevel "ERROR"
        
        # Analisis de error comun
        $errorOutput = $restoreOutput | Out-String
        if ($errorOutput -match "Unable to load the service index for source") {
            Write-DetailedLog "DIAGNOSTICO: Fuente inaccesible detectada" -Color "Yellow" -LogLevel "WARN"
            Write-DetailedLog "   Sugerencia: Considera usar --ignore-failed-sources" -Color "Yellow" -LogLevel "WARN"
        } elseif ($errorOutput -match "SSL|certificate|security") {
            Write-DetailedLog "DIAGNOSTICO: Problema de SSL/certificado" -Color "Yellow" -LogLevel "WARN"
            Write-DetailedLog "   Sugerencia: Verifica la configuracion de SSL" -Color "Yellow" -LogLevel "WARN"
        } elseif ($errorOutput -match "timeout|timed out") {
            Write-DetailedLog "DIAGNOSTICO: Timeout en la conexion" -Color "Yellow" -LogLevel "WARN"
            Write-DetailedLog "   Sugerencia: Aumenta el tiempo de espera o verifica la red" -Color "Yellow" -LogLevel "WARN"
        } elseif ($errorOutput -match "404|Not Found") {
            Write-DetailedLog "DIAGNOSTICO: Paquete no encontrado" -Color "Yellow" -LogLevel "WARN"
            Write-DetailedLog "   Sugerencia: Verifica que el paquete exista en las fuentes configuradas" -Color "Yellow" -LogLevel "WARN"
        }
        
        if ($attempt -lt $maxAttempts) {
            Write-DetailedLog "Esperando $waitSeconds segundos antes del siguiente intento..." -Color "Magenta" -LogLevel "WAIT"
            
            # Mostrar cuenta regresiva
            for ($i = $waitSeconds; $i -gt 0; $i--) {
                Write-Progress -Activity "Esperando para reintentar" -Status "$i segundos restantes" -PercentComplete (($waitSeconds - $i) / $waitSeconds * 100)
                Start-Sleep -Seconds 1
            }
            Write-Progress -Activity "Esperando para reintentar" -Completed
        }
    }
}  # <--- ESTA LLAVE CIERRA EL FOR

# ==================== RESUMEN FINAL ====================

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-DetailedLog "RESUMEN FINAL DEL PROCESO" -Color "Magenta" -LogLevel "SUMMARY"
Write-Host "============================================================" -ForegroundColor Cyan

if ($success) {
    Write-DetailedLog "Estado: RESTAURACION EXITOSA" -Color "Green" -LogLevel "SUMMARY"
} else {
    Write-DetailedLog "Estado: RESTAURACION FALLIDA" -Color "Red" -LogLevel "SUMMARY"
}

Write-DetailedLog "Intentos totales: $attemptCount" -Color "White" -LogLevel "SUMMARY"
Write-DetailedLog "Intentos exitosos: $(($attemptHistory | Where-Object { $_.Success }).Count)" -Color "White" -LogLevel "SUMMARY"
Write-DetailedLog "Intentos fallidos: $(($attemptHistory | Where-Object { -not $_.Success }).Count)" -Color "White" -LogLevel "SUMMARY"

if ($attemptHistory.Count -gt 0) {
    $avgDuration = ($attemptHistory | Measure-Object -Property Duration -Average).Average
    $minDuration = ($attemptHistory | Measure-Object -Property Duration -Minimum).Minimum
    $maxDuration = ($attemptHistory | Measure-Object -Property Duration -Maximum).Maximum
    
    Write-DetailedLog "Tiempo promedio por intento: $([math]::Round($avgDuration, 2)) segundos" -Color "Cyan" -LogLevel "SUMMARY"
    Write-DetailedLog "Tiempo minimo: $([math]::Round($minDuration, 2)) segundos" -Color "Cyan" -LogLevel "SUMMARY"
    Write-DetailedLog "Tiempo maximo: $([math]::Round($maxDuration, 2)) segundos" -Color "Cyan" -LogLevel "SUMMARY"
}

# Estado final de cache
Write-DetailedLog "ESTADO FINAL DE CACHE" -Color "Yellow" -LogLevel "SUMMARY"
$finalCache = Get-NuGetCacheInfo
if (-not $finalCache.Error) {
    $finalSizeMB = [math]::Round($finalCache.TotalSize / 1MB, 2)
    Write-DetailedLog "Tamano final: $finalSizeMB MB" -Color "White" -LogLevel "SUMMARY"
    Write-DetailedLog "Paquetes totales: $($finalCache.PackageCount)" -Color "White" -LogLevel "SUMMARY"
} else {
    Write-DetailedLog "No se pudo obtener informacion de la cache: $($finalCache.Error)" -Color "Yellow" -LogLevel "WARN"
}

# Mostrar historial de intentos en formato tabla
Write-DetailedLog "HISTORIAL DE INTENTOS:" -Color "Yellow" -LogLevel "SUMMARY"

$tableLines = Format-TableAsString -Data $attemptHistory -Columns @(
    @{ Label = "Intento"; Expression = { $_.Attempt } },
    @{ Label = "Duracion (s)"; Expression = { [math]::Round($_.Duration, 2) } },
    @{ Label = "Codigo"; Expression = { $_.ExitCode } },
    @{ Label = "Resultado"; Expression = { if ($_.Success) { "EXITO" } else { "FALLO" } } }
)

foreach ($line in $tableLines) {
    Write-DetailedLog "  $line" -Color "White" -LogLevel "SUMMARY"
}

# Recomendaciones
Write-DetailedLog "RECOMENDACIONES:" -Color "Yellow" -LogLevel "SUMMARY"
if (-not $success) {
    Write-DetailedLog "   1. Verifica tu conexion a internet" -Color "White" -LogLevel "SUMMARY"
    Write-DetailedLog "   2. Verifica que las fuentes NuGet esten accesibles" -Color "White" -LogLevel "SUMMARY"
    Write-DetailedLog "   3. Considera usar: dotnet restore --ignore-failed-sources" -Color "White" -LogLevel "SUMMARY"
    Write-DetailedLog "   4. Limpia la cache: dotnet nuget locals all --clear" -Color "White" -LogLevel "SUMMARY"
    Write-DetailedLog "   5. Revisa el archivo de log para mas detalles: $logFile" -Color "White" -LogLevel "SUMMARY"
} else {
    if ($attemptCount -gt 1) {
        Write-DetailedLog "   La restauracion se completo despues de $attemptCount intentos" -Color "Green" -LogLevel "SUMMARY"
        Write-DetailedLog "   Verifica la estabilidad de tu conexion a internet" -Color "Yellow" -LogLevel "SUMMARY"
    } else {
        Write-DetailedLog "   Restauracion exitosa en el primer intento" -Color "Green" -LogLevel "SUMMARY"
    }
}

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-DetailedLog "Log completo guardado en: $logFile" -Color "Cyan" -LogLevel "SUMMARY"
Write-Host "============================================================" -ForegroundColor Cyan

# ==================== CODIGO DE SALIDA ====================
if (-not $success) {
    Write-DetailedLog "Proceso finalizado con errores" -Color "Red" -LogLevel "ERROR"
    exit 1
} else {
    Write-DetailedLog "Proceso finalizado exitosamente" -Color "Green" -LogLevel "SUCCESS"
    exit 0
}