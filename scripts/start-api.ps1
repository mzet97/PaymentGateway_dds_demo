$host.UI.RawUI.WindowTitle = "PaymentGateway API"
Write-Host "Starting PaymentGateway API in WSL..." -ForegroundColor Cyan
wsl bash /mnt/e/TI/git/tese/CycloneDds.NET/demo/PaymentGateway/scripts/run-local.sh
Write-Host "API stopped." -ForegroundColor Red
Read-Host "Press Enter to exit"
