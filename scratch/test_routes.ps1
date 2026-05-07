$baseUrl = "http://localhost:5007/api"

function Test-Route($method, $path, $body, $token = $null) {
    $url = "$baseUrl/$path"
    $headers = @{ "Content-Type" = "application/json" }
    if ($token) { $headers.Add("Authorization", "Bearer $token") }
    
    try {
        $response = Invoke-RestMethod -Uri $url -Method $method -Headers $headers -Body (ConvertTo-Json $body) -ErrorAction Stop
        return $response
    } catch {
        $msg = $_.Exception.Message
        Write-Error "Error calling $url : $msg"
        return $null
    }
}

# 1. Register
$registerData = @{
    Title = "Mr"
    FirstName = "Admin"
    LastName = "User"
    Email = "admin@example.com"
    PhoneNumber = "1234567890"
    Password = "Password123!"
    ConfirmPassword = "Password123!"
    Role = "Admin"
    AcceptTerms = $true
}
Write-Host "Registering admin..."
$regResponse = Test-Route "POST" "Accounts/register" $registerData
Write-Host "Register response: $($regResponse | ConvertTo-Json)"

# 2. Authenticate
$authData = @{
    Email = "admin@example.com"
    Password = "Password123!"
}
Write-Host "Authenticating..."
$authResponse = Test-Route "POST" "Accounts/authenticate" $authData
Write-Host "Auth response: $($authResponse | ConvertTo-Json)"
$token = $authResponse.jwtToken
Write-Host "Token received: $token"

if ($token) {
    # 3. Create a Room
    $ts = Get-Date -Format "HHmmss"
    $roomData = @{
        RoomName = "Test Room $ts"
        RoomNumber = [int]$ts
        Floor = 1
        Building = "Main"
        DeviceId = "DEVICE_$ts"
    }
    Write-Host "Creating room..."
    $roomResponse = Test-Route "POST" "room" $roomData $token
    Write-Host "Room response: $($roomResponse | ConvertTo-Json)"

    # 4. Get All Rooms
    Write-Host "Getting all rooms..."
    $allRooms = Test-Route "GET" "room" $null $token
    Write-Host "Rooms count: $($allRooms.count)"

    # 5. Log an event (from Arduino)
    $logData = @{
        DeviceId = "DEVICE_001"
        Event = "card_on"
        Details = "Test card tap"
        CardUID = "CARD_123"
    }
    Write-Host "Logging event..."
    $logResponse = Test-Route "POST" "arduino/log" $logData
    Write-Host "Log response: $($logResponse | ConvertTo-Json)"

    # 6. Get logs
    Write-Host "Getting logs..."
    $logsResponse = Test-Route "GET" "arduino/log" $null $token
    Write-Host "Logs count: $($logsResponse.logs.Count)"
}

# Note: Need to verify email manually via DB for now
