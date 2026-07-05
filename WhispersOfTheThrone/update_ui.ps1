$content = Get-Content 'MainForm.cs' -Raw -Encoding utf8
$content = $content -replace 'AddActionButton\("Alt\+1', "if (state.Factions != null && state.Factions.Any(f => f.IsUltimatumPending && !f.IsRebellionStarted))`n            {`n                AddActionButton(`"[هام جداً]: الرد على الإنذار النهائي!`", (s, evt) => ShowFactionsMenu(null, null));`n            }`n            AddActionButton(`"Alt+1"
Set-Content 'MainForm.cs' -Value $content -Encoding utf8
