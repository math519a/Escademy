﻿using PayPal.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Escademy.Models
{

    public static class PayPalConfiguration
    {
        public readonly static string ClientId;
        public readonly static string ClientSecret;

        // Static constructor for setting the readonly static members.
        static PayPalConfiguration()
        {
            var config = GetConfig();
            ClientId = config["clientId"];
            ClientSecret = config["clientSecret"];
        }

        // Create the configuration map that contains mode and other optional configuration details.
        public static Dictionary<string, string> GetConfig()
        {
            // ConfigManager.Instance.GetProperties(); // it doesn't work on ASPNET 5
            return new Dictionary<string, string>() {
                { "clientId", "AT5TrWZksxuAIexnxpY5SV_pYKLbRCEGFH4dAVs0FmHPYGQ9_qjEVhdHTGYl3JOe1PugX3Ia4_mHXynh" },
                { "clientSecret", "EChFdfBAYppQLTY6iF4wOK3qYknMsC_W5v3PxUWyCloBce8txPbeXD7fWuGEPsrEmCoaE7vA3F-mzmp0" }
            };
        }

        // Create accessToken
        private static string GetAccessToken()
        {
            // ###AccessToken
            // Retrieve the access token from OAuthTokenCredential by passing in
            // ClientID and ClientSecret
            // It is not mandatory to generate Access Token on a per call basis.
            // Typically the access token can be generated once and reused within the expiry window                
            string accessToken = new OAuthTokenCredential
                (ClientId, ClientSecret, GetConfig()).GetAccessToken();
            return accessToken;
        }

        // Returns APIContext object
        public static APIContext GetAPIContext(string accessToken = "")
        {
            // Pass in a `APIContext` object to authenticate 
            // the call and to send a unique request id 
            // (that ensures idempotency). The SDK generates
            // a request id if you do not pass one explicitly. 
            var apiContext = new APIContext(string.IsNullOrEmpty(accessToken) ?
                GetAccessToken() : accessToken);
            apiContext.Config = GetConfig();

            return apiContext;
        }


    }
}
