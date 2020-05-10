#Requires -Version 3.0

Param(
    [string] [Parameter(Mandatory = $true)] $ResourceGroupLocation,
    $context = $null
)

Function Select-Context() {
    [OutputType([Microsoft.Azure.Commands.Profile.Models.Core.PSAzureContext])]
    Param([Microsoft.Azure.Commands.Profile.Models.Core.PSAzureContext] $context)

    if (!$context) {
        try {
            $connection = Connect-AzAccount -Environment "AzureCloud" `
                -ErrorAction Stop
            $context = $connection.Context
        }
        catch {
            throw "The login to the Azure account was not successful."
        }
    }

    $subscriptions = Get-AzSubscription | Where-Object { $_.State -eq "Enabled" }

    if ($subscriptions.Count -eq 0) {
        throw "No active subscriptions found - exiting."
    }
    elseif ($subscriptions.Count -eq 1) {
        $subscriptionId = $subscriptions[0].Id
    }
    else {
        Write-Host "Please choose a subscription from this list (using its Index):"
        $script:index = 0
        $subscriptions | Format-Table -AutoSize -Property `
        @{Name = "Index"; Expression = { ($script:index++) } }, `
        @{Name = "Subscription"; Expression = { $_.Name } }, `
        @{Name = "Id"; Expression = { $_.SubscriptionId } }`
        | Out-Host
        while ($true) {
            $option = Read-Host ">"
            try {
                if ([int]$option -ge 1 -and [int]$option -le $subscriptions.Count) {
                    break
                }
            }
            catch {
                Write-Host "Invalid index '$($option)' provided."
            }
            Write-Host "Choose from the list using an index between 1 and $($subscriptions.Count)."
        }
        $subscriptionId = $subscriptions[$option - 1].Id
    }
    $subscriptionDetails = Get-AzSubscription -SubscriptionId $subscriptionId
    if (!$subscriptionDetails) {
        throw "Failed to get details for subscription $($subscriptionId)"
    }

    Write-Host "Azure subscription $($context.Subscription.Name) ($($context.Subscription.Id)) selected."
    return $context
}

#*******************************************************************************************************
# Get or create new resource group for deployment
#*******************************************************************************************************
Function Select-ResourceGroup() {

    $first = $true
    while ([string]::IsNullOrEmpty($script:ResourceGroupName) `
            -or ($script:ResourceGroupName -notmatch "^[a-z0-9-_]*$")) {
        if ($first -eq $false) {
            Write-Host "Use alphanumeric characters as well as '-' or '_'."
        }
        else {
            Write-Host
            Write-Host "Please provide a name for the resource group."
            $first = $false
        }
        $script:ResourceGroupName = Read-Host -Prompt ">"
    }

    $resourceGroup = Get-AzResourceGroup -Name $script:ResourceGroupName `
        -ErrorAction SilentlyContinue
    if (!$resourceGroup) {
        Write-Host "Resource group '$script:ResourceGroupName' does not exist."
        $resourceGroup = New-AzResourceGroup -Name $script:ResourceGroupName `
            -Location $script:ResourceGroupLocation
        Write-Host "Created new resource group $($script:ResourceGroupName) in $($script:ResourceGroupLocation)."
        return $True
    }
    else {
        Write-Host "Using existing resource group $($script:ResourceGroupName)..."
        return $False
    }
}

#******************************************************************************
# Generate a random password
#******************************************************************************
Function New-Password() {
    param(
        $length = 15
    )
    $punc = 46..46
    $digits = 48..57
    $lcLetters = 65..90
    $ucLetters = 97..122
    $password = `
        [char](Get-Random -Count 1 -InputObject ($lcLetters)) + `
        [char](Get-Random -Count 1 -InputObject ($ucLetters)) + `
        [char](Get-Random -Count 1 -InputObject ($digits)) + `
        [char](Get-Random -Count 1 -InputObject ($punc))
    $password += get-random -Count ($length - 4) `
        -InputObject ($punc + $digits + $lcLetters + $ucLetters) |`
        ForEach-Object -begin { $aa = $null } -process { $aa += [char]$_ } -end { $aa }

    return $password
}

#*******************************************************************************************************
# Deploy azuredeploy.json
#*******************************************************************************************************
Function New-Deployment() {
    [OutputType([System.Object[]])]
    Param($context)

    $templateParameters = @{ }

    # Get all vm skus available in the location and in the account
    $availableVms = Get-AzComputeResourceSku | Where-Object {
        ($_.ResourceType.Contains("virtualMachines")) -and `
        ($_.Locations -icontains $script:ResourceGroupLocation) -and `
        ($_.Restrictions.Count -eq 0)
    }
    # Sort based on sizes and filter minimum requirements
    $availableVmNames = $availableVms `
    | Select-Object -ExpandProperty Name -Unique
    $vmSizes = Get-AzVMSize $script:ResourceGroupLocation `
    | Where-Object { $availableVmNames -icontains $_.Name } `
    | Where-Object {
        ($_.NumberOfCores -ge 2) -and `
        ($_.MemoryInMB -ge 8192) -and `
        ($_.OSDiskSizeInMB -ge 1047552) -and `
        ($_.ResourceDiskSizeInMB -gt 8192)
    } `
    | Sort-Object -Property `
        NumberOfCores, MemoryInMB, ResourceDiskSizeInMB, Name
    # Pick top
    if ($vmSizes.Count -ne 0) {
        $vmSize = $vmSizes[0].Name
        Write-Host "Using $($vmSize) as VM size for all edge simulation hosts..."
        $templateParameters.Add("VmSize", $vmSize)
    }
    
    $adminUser = "azureuser"
    $adminPassword = New-Password
    $templateParameters.Add("adminPassword", $adminPassword)
    $templateParameters.Add("adminUsername", $adminUser)
    
    while ($true) {
        try {
            Write-Host "Starting deployment..."
    
            if (![string]::IsNullOrEmpty($adminUser) -and ![string]::IsNullOrEmpty($adminPassword)) {
                Write-Host
                Write-Host "To troubleshoot simulation use the following User and Password to log into the VM's:"
                Write-Host
                Write-Host $adminUser
                Write-Host $adminPassword
                Write-Host
            }

            # Accept terms for ARM marketplace IoT Edge machine
            Get-AzMarketplaceTerms -Publisher "microsoft_iot_edge" -Product "iot_edge_vm_ubuntu" -Name "ubuntu_1604_edgeruntimeonly" | Set-AzMarketplaceTerms -Accept

            # Start the deployment
            $templateFilePath = Join-Path $ScriptDir "azuredeploy.json"
            $deployment = New-AzResourceGroupDeployment -ResourceGroupName $ResourceGroupName `
                -TemplateFile $templateFilePath -TemplateParameterObject $templateParameters
    
            if ($deployment.ProvisioningState -ne "Succeeded") {
                throw "Deployment $($deployment.ProvisioningState)."
            }
    
            Write-Host "Deployment succeeded."
            return $deployment
        }
        catch {
            $ex = $_
            Write-Host $_.Exception.Message
            Write-Host "Deployment failed."
    
            $deleteResourceGroup = $false
            $retry = Read-Host -Prompt "Try again? [y/n]"
            if ($retry -match "[yY]") {
                continue
            }
            if ($script:deleteOnErrorPrompt) {
                $reply = Read-Host -Prompt "Delete resource group? [y/n]"
                $deleteResourceGroup = ($reply -match "[yY]")
            }

            if ($deleteResourceGroup) {
                try {
                    Write-Host "Removing resource group $($script:ResourceGroupName)..."
                    Remove-AzResourceGroup -ResourceGroupName $script:ResourceGroupName -Force
                }
                catch {
                    Write-Warning $_.Exception.Message
                }
            }
            throw $ex
        }
    }
}

Function New-IoTEdgeDevice() {
    Param([System.Object[]] $deployment)

    Write-Host "Registering IoT Edge Device..."

    $dummydevice = "itsdemomdummy"
    $iotHubName = $deployment.Outputs["iotHubName"].Value
    $edgeVmName = $deployment.Outputs["edgeVmName"].Value

    # Need to create a dummy child device because there is a bug in Add-AzIotHubDevice command
    # https://github.com/Azure/azure-powershell/issues/11597
    Add-AzIotHubDevice -ResourceGroupName $script:ResourceGroupName `
        -IotHubName $iotHubName -DeviceId $dummydevice -AuthMethod "shared_private_key"

    Add-AzIotHubDevice -ResourceGroupName $script:ResourceGroupName `
        -IotHubName $iotHubName -DeviceId $edgeVmName `
        -AuthMethod "shared_private_key" -Children $dummydevice -EdgeEnabled

    Write-Host "Providing Device ConnectionString to VM..."
    
    $edgeConnectionString = $(Get-AzIotHubDCS -ResourceGroupName $script:ResourceGroupName `
            -IotHubName $iotHubName -DeviceId $edgeVmName -KeyType primary).ConnectionString

    Invoke-AzVMRunCommand -ResourceGroupName $script:ResourceGroupName `
        -VMName $edgeVmName -CommandId "RunShellScript" `
        -ScriptPath "configedge.sh" -Parameter @{param1 = "'$edgeConnectionString'" }

    Write-Host "Successfully provisioned Edge Device!"
}


Function New-SASToken
{
    Param(
        [Parameter(Mandatory=$True)]
        [string]$ResourceUri,
        [Parameter(Mandatory=$True)]
        [string]$Key,
        [string]$KeyName="",
        [int]$TokenTimeOut=1800 # in seconds
    )
    [Reflection.Assembly]::LoadWithPartialName("System.Web") | out-null
   
    $Expires = ([DateTimeOffset]::Now.ToUnixTimeSeconds())+$TokenTimeOut
    
    $SignatureString=[System.Web.HttpUtility]::UrlEncode($ResourceUri)+ "`n" + [string]$Expires
    $HMAC = New-Object System.Security.Cryptography.HMACSHA256
    $HMAC.key = [Convert]::FromBase64String($Key)
    $Signature = $HMAC.ComputeHash([Text.Encoding]::ASCII.GetBytes($SignatureString))
    $Signature = [Convert]::ToBase64String($Signature)

    $SASToken = "SharedAccessSignature sr=" + [System.Web.HttpUtility]::UrlEncode($ResourceUri) + "&sig=" `
                 + [System.Web.HttpUtility]::UrlEncode($Signature) + "&se=" + $Expires
    
    if ($KeyName -ne "")
    {
        $SASToken=$SASToken+"&skn=$KeyName"
    }
    return $SASToken
}


Function Deploy-IoTEdgeConfiguration(){
    Param([System.Object[]] $deployment)

    Write-Host "Pushing new deployment to IoT Edge Device..."

    $keyName = "iothubowner"
    $iotHubName = $deployment.Outputs["iotHubName"].Value
    $edgeVmName = $deployment.Outputs["edgeVmName"].Value
    $ownerkey = (Get-AzIotHubKey -ResourceGroupName $script:ResourceGroupName -Name $iotHubName -KeyName $keyName).PrimaryKey

    $templateManifest = Join-Path $ScriptDir "deployment.demo.json"
    $body = Get-Content -Raw -Path $templateManifest

    ##TODO document why doing through REST API and github issue for missing Cmdlet in AZ
    
    $resourceUri = "$iotHubName.azure-devices.net/devices/$([System.Web.HttpUtility]::UrlEncode($edgeVmName))"

    [Reflection.Assembly]::LoadWithPartialName("System.Web") | out-null

    try
    {
        
        $sas = New-SASToken -ResourceUri $resourceUri -Key $ownerkey -KeyName $keyName
        
        $webRequest = Invoke-WebRequest -Method POST `
            -Uri "https://$resourceUri/applyConfigurationContent?api-version=2019-10-01" `
            -ContentType "application/json" -Header @{ Authorization = $sas} -Body $body -UseBasicParsing

        Write-Host "Successfully pushed deployment to IoT Edge Device!"

    } 
    catch [System.Net.WebException]
    {
        Write-Error "An exception was caught: $($_.Exception.Message)"
    }

}



#*******************************************************************************************************
# Script body
#*******************************************************************************************************
$ErrorActionPreference = "Stop"
$script:ScriptDir = Split-Path $script:MyInvocation.MyCommand.Path

if ($null -eq (Get-InstalledModule -Name "Az" -MinimumVersion 3.7.0 -ErrorAction SilentlyContinue)) {
    Write-Host "Az Module version 3.7.0 or higher is required to run this script!"
    Write-Host "Please install like this: 'Install-Module -Name Az -RequiredVersion 3.7.0 -Scope CurrentUser -Force -AllowClobber'"
    Write-Host "For more information: https://docs.microsoft.com/en-us/powershell/azure/install-az-ps?view=azps-3.7.0#install-the-azure-powershell-module"
    Exit
}

Write-Host "Signing in ..."
Write-Host
Import-Module Az

$script:context = Select-Context -context $script:context
$script:deleteOnErrorPrompt = Select-ResourceGroup
$script:deployment = New-Deployment -context $script:context
New-IoTEdgeDevice $script:deployment
Deploy-IoTEdgeConfiguration $script:deployment

