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
        default                       { $_.Key }
    }
    $filasModelos += "| $nombre | $($_.Value) | $pct% |`n"
}

$agentesTxt = ($agentesConUso.Keys | Sort-Object) -join ", "
$costoReal = if ($totCost -gt 0) { "$" + $totCost.ToString("0.00", $n) } else { '$0.00 (modelo ZAI sin cargo reportado)' }

$seccion = @"
## 🤖 Uso de IA en el desarrollo

> Sección actualizada automáticamente en cada commit con `docs/actualizar-stats-ia.ps1`.
> Los datos salen de los archivos de sesión del gateway (AutoClaw/OpenClaw): tokens,
> modelos y costos reportados por el proveedor, más los prompts escritos por el
> desarrollador (marcados como solicitudes de usuario).

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

### Plataforma

- **OpenClaw / AutoClaw** (gateway local), API compatible `openai-completions`.
- Los modelos se sirven vía **ZAI** (ruteador `zai_auto` elige el modelo según la tarea; también se usaron DeepSeek V4 Flash y GLM-5 Turbo).
- Herramientas auxiliares de IA: AutoGLM (reconocimiento visual de capturas) y scripts UIA locales.

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
