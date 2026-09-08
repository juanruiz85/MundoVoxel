# actualizar-stats-ia.ps1
# Genera la seccion "Uso de IA en el desarrollo" del readme.md a partir de los
# archivos de sesion del gateway (AutoClaw/OpenClaw). Ejecutar desde la raiz del
# repositorio antes de cada commit:
#   powershell -ExecutionPolicy Bypass -File docs/actualizar-stats-ia.ps1
param(
    [string]$SessionsRoot = "$env:USERPROFILE\.openclaw-autoclaw\agents",
    [string]$ReadmePath = "readme.md"
)

$ErrorActionPreference = "Stop"
if (!(Test-Path $ReadmePath)) { Write-Error "No encuentro $ReadmePath. Ejecuta desde la raiz del repo."; exit 1 }

# --- Recopilar datos de todas las sesiones de todos los agentes ------------
$totIn = [long]0; $totOut = [long]0; $totCache = [long]0; $totCost = [double]0
$prompts = 0; $sysRem = 0; $asst = 0; $sesiones = 0; $agentesConUso = @{}
$modelos = @{}

Get-ChildItem "$SessionsRoot\*\sessions\*.jsonl" -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -notmatch "trajectory" } |
    ForEach-Object {
        $sesiones++
        $agente = Split-Path (Split-Path $_.DirectoryName -Parent) -Leaf
        Get-Content $_.FullName -ErrorAction SilentlyContinue | ForEach-Object {
            try { $j = $_ | ConvertFrom-Json } catch { return }
            if ($j.type -eq "message") {
                if ($j.message.role -eq "user") {
                    $txt = ($j.message.content -join " ")
                    if ($txt -match "AUTOCLAW_USER_AUTHORED_REQUEST_START") { $prompts++; $agentesConUso[$agente] = $true }
                    else { $sysRem++ }
                }
                if ($j.message.role -eq "assistant") {
                    $asst++
                    $u = $j.message.usage
                    if ($u) {
                        $totIn   += [long]$u.input
                        $totOut  += [long]$u.output
                        $totCache+= [long]$u.cacheRead
                        $totCost += [double]$u.cost.total
                    }
                    $m = "$($j.message.provider)/$($j.message.model)"
                    if ($modelos.ContainsKey($m)) { $modelos[$m]++ } else { $modelos[$m] = 1 }
                }
            }
        }
    }

# --- Periodo (primera y ultima sesion) --------------------------------------
$primera = (Get-ChildItem "$SessionsRoot\*\sessions\*.jsonl" -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -notmatch "trajectory" } | ForEach-Object {
        try { (Get-Content $_.FullName -TotalCount 1 | ConvertFrom-Json).timestamp } catch { $null }
    } | Where-Object { $_ } | Sort-Object | Select-Object -First 1)
$ultima = (Get-ChildItem "$SessionsRoot\*\sessions\*.jsonl" -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -notmatch "trajectory" } | ForEach-Object {
        try { (Get-Content $_.FullName -Tail 1 | ConvertFrom-Json).timestamp } catch { $null }
    } | Where-Object { $_ } | Sort-Object | Select-Object -Last 1)
$periodo = "$(([datetime]$primera).ToString('yyyy-MM-dd')) → $(([datetime]$ultima).ToString('yyyy-MM-dd'))"


# --- Uso vía ZCode CLI (capa de código delegada) -----------------------------
# ZCode hace sus propias llamadas al modelo y las registra en
# ~/.zcode/cli/rollout/model-io-*.jsonl (modelo + tokens por llamada). El
# gateway no ve este consumo: se suma aparte para reportarlo honestamente.
$zcodeLlamadas = 0; $zcodeIn = [long]0; $zcodeOut = [long]0; $zcodeCache = [long]0
$zcodeModelos = @{}; $zcodeInM = @{}; $zcodeOutM = @{}; $zcodeCacheM = @{}
$zcodeDir = Join-Path $HOME ".zcode\cli\rollout"
if (Test-Path $zcodeDir) {
    Get-ChildItem $zcodeDir -Filter "model-io-*.jsonl" -ErrorAction SilentlyContinue | ForEach-Object {
        Get-Content $_.FullName -ErrorAction SilentlyContinue | ForEach-Object {
            try { $j = $_ | ConvertFrom-Json } catch { return }
            $modelId = $null
            if ($j.model -and $j.model.modelId) { $modelId = $j.model.modelId }
            if (-not $modelId) { return }
            $zcodeLlamadas++
            if (-not $zcodeModelos.ContainsKey($modelId)) { $zcodeModelos[$modelId] = 0 }
            $zcodeModelos[$modelId]++
            $u = $null
            if ($j.response -and $j.response.usage) { $u = $j.response.usage }
            if (-not $u -and $j.usage) { $u = $j.usage }
            if ($u) {
                $i2 = 0; $o2 = 0; $c2 = 0
                if ($u.inputTokens)      { $i2 = [long]$u.inputTokens }
                if ($u.outputTokens)     { $o2 = [long]$u.outputTokens }
                if ($u.cacheReadTokens)  { $c2 = [long]$u.cacheReadTokens }
                $zcodeIn += $i2; $zcodeOut += $o2; $zcodeCache += $c2
                $zcodeInM[$modelId] = [long]$zcodeInM[$modelId] + $i2
                $zcodeOutM[$modelId] = [long]$zcodeOutM[$modelId] + $o2
                $zcodeCacheM[$modelId] = [long]$zcodeCacheM[$modelId] + $c2
            }
        }
    }
}
# --- Estimacion a tarifas de mercado (modelos equivalentes de razonamiento) -
$precioIn = 2.00; $precioOut = 8.00; $precioCache = 0.10   # USD por millon
$estIn   = $totIn   / 1e6 * $precioIn
$estOut  = $totOut  / 1e6 * $precioOut
$estCache= $totCache/ 1e6 * $precioCache
$estTotal= $estIn + $estOut + $estCache
$totTokens = $totIn + $totOut

$n = [System.Globalization.CultureInfo]::InvariantCulture
function Fmt([long]$v) { return $v.ToString("N0", $n) }

# --- Modelos: tabla ---------------------------------------------------------
$filasModelos = ""
$modelos.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object {
    $pct = if ($asst -gt 0) { [math]::Round(100.0 * $_.Value / $asst, 1) } else { 0 }
    $nombre = switch -Regex ($_.Key) {
        "zai_auto"                    { "zai_auto (ruteo automático)" }
        "dpskpro_deepseek-v4-flash"   { "dpskpro_deepseek-v4-flash (DeepSeek V4 Flash)" }
        "zai_glm-5-turbo"             { "zai_glm-5-turbo (GLM-5 Turbo)" }
        "gateway-injected"            { "gateway-injected (mensaje interno)" }
        default                       { if ($_ -eq $null -or $_.Trim('/') -eq '') { "(sin identificar en la sesión)" } else { $_ } }
    }
    $filasModelos += "| $nombre | $($_.Value) | $pct% |`n"
}

$filasZcode = ""
$zcodeModelos.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object {
    $k = $_.Key
    $nombreZ = switch -Regex ($k) {
        "deepseek-v4-pro" { "$k (DeepSeek V4 Pro)" }
        default           { $k }
    }
    $inM = [long]$zcodeInM[$k]; $outM = [long]$zcodeOutM[$k]; $cM = [long]$zcodeCacheM[$k]
    $filasZcode += "| $nombreZ | $($_.Value) | $(Fmt $inM) | $(Fmt $outM) | $(Fmt $cM) |`n"
}
$seccionZcode = ""
if ($zcodeLlamadas -gt 0) {
$seccionZcode = @"

### Uso vía ZCode CLI (capa de código delegada)

El agente delega tareas de código al **ZCode CLI**, que hace sus propias llamadas
al modelo. Ese consumo no pasa por el gateway: se registra en
``~/.zcode/cli/rollout/model-io-*.jsonl`` y se reporta aquí aparte.

| Modelo | Llamadas | Tokens entrada | Tokens salida | Caché leída |
|---|---|---|---|---|
$($filasZcode.TrimEnd())

- Total: $($zcodeLlamadas) llamadas - $(Fmt $zcodeIn) tokens de entrada - $(Fmt $zcodeOut) de salida - $(Fmt $zcodeCache) de cache leida.
"@
}
$agentesTxt = ($agentesConUso.Keys | Sort-Object) -join ", "
$costoReal = if ($totCost -gt 0) { "$" + $totCost.ToString("0.00", $n) } else { '$0.00 (modelo ZAI sin cargo reportado)' }

$seccion = @"
## 🤖 Uso de IA en el desarrollo

> Sección actualizada automáticamente en cada commit con `docs/actualizar-stats-ia.ps1`.
> Los datos salen de los archivos de sesión del gateway (AutoClaw/OpenClaw): tokens,
> modelos y costos reportados por el proveedor, más los prompts escritos por el
> desarrollador (marcados como solicitudes de usuario). El consumo del **ZCode CLI**
> (capa de código delegada) se añade aparte: el gateway no lo ve.

### Resumen

| Métrica | Valor |
|---|---|
| Período de desarrollo | $periodo |
| Sesiones de IA | $($sesiones) |
| Prompts del desarrollador | $(Fmt $prompts) |
| Respuestas generadas por IA | $(Fmt $asst) |
| Tokens de entrada (prompts + contexto) | $(Fmt $totIn) |
| Tokens de salida (generación) | $(Fmt $totOut) |
| **Tokens totales** | **$(Fmt $totTokens)** |
| Tokens de caché leídos | $(Fmt $totCache) |
| Costo real registrado | $costoReal |
| Costo estimado a tarifas de mercado | ~$(($estTotal).ToString("0.00", $n)) USD |
| Agentes de IA con uso | $agentesTxt |

### Promedios

- Tokens por prompt: ~$(([math]::Round($totIn / [math]::Max(1,$prompts))).ToString("N0", $n)) de entrada / ~$(([math]::Round($totOut / [math]::Max(1,$prompts))).ToString("N0", $n)) de salida.
- Costo estimado por prompt: ~$(($estTotal / [math]::Max(1,$prompts)).ToString("0.00", $n)) USD (a tarifas de mercado).

### Modelos utilizados

| Modelo | Respuestas | % del total |
|---|---|---|
$($filasModelos.TrimEnd())
$seccionZcode

### Plataforma

- **OpenClaw / AutoClaw** (gateway local), API compatible `openai-completions`.
- Los modelos se sirven vía **ZAI** (ruteador `zai_auto` elige el modelo según la tarea; también se usaron DeepSeek V4 Flash y GLM-5 Turbo).
- Herramientas auxiliares de IA: AutoGLM (reconocimiento visual de capturas), scripts UIA locales
  y **ZCode CLI** (implementación de código, con DeepSeek V4 Pro; ver desglose arriba).

### Nota metodológica

- "Tokens de entrada" incluye el contexto completo reenviado en cada turno (por eso es
  muy superior a los tokens de salida). "Caché leída" son tokens reutilizados del contexto
  previo (tarifa reducida en proveedores comerciales).
- El **costo real registrado es `$0.00`** porque el proveedor ZAI no reporta cargos para
  estos modelos; la columna "estimado a tarifas de mercado" usa `$2/M` entrada,
  `$8/M` salida y `$0.10/M` caché (referencia típica de modelos de razonamiento) solo como
  orientación.
"@

# --- Reemplazar la seccion entre marcadores en readme.md --------------------
$marcaI = "<!-- IA-USO-INICIO -->"
$marcaF = "<!-- IA-USO-FIN -->"
$contenido = [System.IO.File]::ReadAllText((Resolve-Path $ReadmePath), [System.Text.Encoding]::UTF8)
if ($contenido.Contains($marcaI) -and $contenido.Contains($marcaF)) {
    $patron = '(?s)' + [regex]::Escape($marcaI) + '.*?' + [regex]::Escape($marcaF)
    $reemplazo = $marcaI + "`n" + $seccion + "`n" + $marcaF
    # MatchEvaluator: evita que "$" del texto se interprete como grupo de reemplazo
    $evaluador = [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $reemplazo }
    $contenido = [regex]::Replace($contenido, $patron, $evaluador)
} else {
    $contenido = $contenido.TrimEnd() + "`n`n---`n`n" + $marcaI + "`n" + $seccion + "`n" + $marcaF + "`n"
}
# UTF-8 con BOM para que cualquier lector (incluido Windows PowerShell) lo muestre bien
[System.IO.File]::WriteAllText((Resolve-Path $ReadmePath), $contenido, [System.Text.Encoding]::UTF8)

Write-Host "OK: readme.md actualizado."
Write-Host "  Sesiones=$sesiones Prompts=$prompts Respuestas=$asst TokensIn=$(Fmt $totIn) TokensOut=$(Fmt $totOut) Cache=$(Fmt $totCache)"
Write-Host "  Costo real=$costoReal | Estimado mercado=$(($estTotal).ToString('0.00', $n)) USD | Agentes: $agentesTxt"
if ($zcodeLlamadas -gt 0) { Write-Host "  ZCode CLI: $zcodeLlamadas llamadas | TokensIn=$(Fmt $zcodeIn) TokensOut=$(Fmt $zcodeOut) Cache=$(Fmt $zcodeCache)" }
