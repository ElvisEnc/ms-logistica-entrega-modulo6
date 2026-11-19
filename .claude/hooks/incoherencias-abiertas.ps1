# incoherencias-abiertas.ps1  ·  SessionStart
# Al abrir sesion, recuerda las incoherencias sin resolver.
$archivo = Join-Path $env:CLAUDE_PROJECT_DIR 'docs\INCOHERENCIAS.md'
if (Test-Path $archivo) {
    # El bloque <!-- --> de la plantilla de ejemplo contiene el mismo texto
    # "**Estado:** ABIERTA" que una entrada real: se descarta antes de contar
    # para no generar un falso positivo contra el propio ejemplo del formato.
    $contenido = Get-Content -Path $archivo -Raw
    $sinPlantilla = [regex]::Replace($contenido, '(?s)<!--.*?-->', '')
    $abiertas = ([regex]::Matches($sinPlantilla, 'Estado:\*\*\s*ABIERTA')).Count
    if ($abiertas -gt 0) {
        @{
            hookSpecificOutput = @{
                hookEventName     = 'SessionStart'
                additionalContext = "ATENCION: hay $abiertas incoherencia(s) ABIERTA(s) en docs/INCOHERENCIAS.md. Leelas antes de escribir codigo que dependa de esos puntos. No las resuelvas por tu cuenta."
            }
        } | ConvertTo-Json -Depth 5
    }
}
exit 0
