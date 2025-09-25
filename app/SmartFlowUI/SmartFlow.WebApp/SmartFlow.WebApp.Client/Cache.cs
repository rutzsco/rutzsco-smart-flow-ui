// Copyright (c) Microsoft. All rights reserved.

namespace SmartFlow.WebApp.Client;

public static class Cache
{
    public static UserInformation? UserInformation = null;

    public static void Clear()
    {
        UserInformation = null;
    }

    public static void SetUserInformation(UserInformation _userInformation)
    {
        UserInformation = _userInformation;
    }
}
