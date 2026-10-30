# ASCII-only (#PS-1). powershell -STA -File gui_step_run.ps1 <dir with exe copy> <paths.txt UTF-8: line1 input, line2 out dir>
param([string]$Dir, [string]$PathsFile)
Add-Type -AssemblyName System.Windows.Forms
$p = Get-Content -Encoding UTF8 $PathsFile
[void][Reflection.Assembly]::LoadFrom((Join-Path $Dir 'Cad2Gdml.Core.dll'))
$ft = [Reflection.Assembly]::LoadFrom((Join-Path $Dir 'Cad2GdmlApp.exe')).GetType('Cad2Gdml.App.MainForm')
$f = [Activator]::CreateInstance($ft)
$f.StartPosition = 'Manual'; $f.Location = New-Object Drawing.Point(-20000, -20000); $f.ShowInTaskbar = $false
$f.Show()
$bf = [Reflection.BindingFlags]'NonPublic,Instance'
function Fld($n) { $ft.GetField($n, $bf).GetValue($f) }
[void](Fld '_lstInputs').Items.Add($p[0])
(Fld '_txtOutDir').Text = $p[1]
(Fld '_chkAllowDefault').Checked = $true
$ft.GetMethod('OnConvert', $bf).Invoke($f, @($null, [EventArgs]::Empty))
$btn = Fld '_btnConvert'; $t0 = Get-Date
do { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 50; $s = ((Get-Date) - $t0).TotalSeconds } while ($s -lt 3 -or (-not $btn.Enabled -and $s -lt 240))
for ($i = 0; $i -lt 40; $i++) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 50 }
'STATUS: ' + (Fld '_lblStatus').Text
'REPORT-LINES: ' + (Fld '_txtReport').Lines.Count
'PICTURE: ' + ((Fld '_picSketch').Image -ne $null)
$f.Close()
