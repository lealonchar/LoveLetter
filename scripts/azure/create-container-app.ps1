param(
    [string]$ResourceGroup = "loveletter-rg",
    [string]$Location = "westeurope",
    [string]$EnvironmentName = "loveletter-env",
    [string]$ContainerAppName = "loveletter-backend",
    [string]$Image = "ghcr.io/YOUR_GITHUB_USERNAME/loveletter-backend:latest",
    [Parameter(Mandatory = $true)]
    [string]$AllowedOrigins,
    [string]$GhcrUsername = "",
    [string]$GhcrToken = ""
)

$ErrorActionPreference = "Stop"

Write-Host "Checking Azure Container Apps CLI extension..."
az extension add --name containerapp --upgrade | Out-Null

Write-Host "Creating resource group '$ResourceGroup' in '$Location'..."
az group create `
    --name $ResourceGroup `
    --location $Location | Out-Null

Write-Host "Creating Container Apps environment '$EnvironmentName'..."
az containerapp env create `
    --name $EnvironmentName `
    --resource-group $ResourceGroup `
    --location $Location | Out-Null

$createArgs = @(
    "containerapp", "create",
    "--name", $ContainerAppName,
    "--resource-group", $ResourceGroup,
    "--environment", $EnvironmentName,
    "--image", $Image,
    "--ingress", "external",
    "--target-port", "8080",
    "--min-replicas", "0",
    "--max-replicas", "1",
    "--cpu", "0.25",
    "--memory", "0.5Gi",
    "--env-vars",
    "AllowedOrigins=$AllowedOrigins",
    "ASPNETCORE_URLS=http://0.0.0.0:8080"
)

if ($GhcrUsername -and $GhcrToken) {
    $createArgs += @(
        "--registry-server", "ghcr.io",
        "--registry-username", $GhcrUsername,
        "--registry-password", $GhcrToken
    )
}

Write-Host "Creating Container App '$ContainerAppName'..."
az @createArgs | Out-Null

$fqdn = az containerapp show `
    --name $ContainerAppName `
    --resource-group $ResourceGroup `
    --query "properties.configuration.ingress.fqdn" `
    --output tsv

Write-Host ""
Write-Host "Backend is available at:"
Write-Host "https://$fqdn"
Write-Host ""
Write-Host "Use this for the frontend:"
Write-Host "VITE_SERVER_URL=https://$fqdn"
