﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Bot.Schema;

namespace Microsoft.Bot.Connector.Authentication
{
    public static class JwtTokenValidation
    {
        private static HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Validates the security tokens required by the Bot Framework Protocol. Throws on any exceptions. On success returns the claims identity.
        /// For requests where auth is disabled and no auth header is present, Anonymous identity is returned. Otherwise, actual claims identity is returned.
        /// </summary>
        /// <param name="activity">The incoming Activity from the Bot Framework or the Emulator</param>
        /// <param name="authHeader">The Bearer token included as part of the request</param>
        /// <param name="credentials">The set of valid credentials, such as the Bot Application ID</param>
        /// <param name="httpClient">Validating an Activity requires validating the claimset on the security token. This 
        /// validation may require outbound calls for Endorsement validation and other checks. Those calls are made to
        /// TLS services, which are (latency wise) expensive resources. The httpClient passed in here, if shared by the layers
        /// above from call to call, enables connection reuse which is a significant performance and resource improvement.</param>
        /// <returns>Claims identity. An anonymous identity is returned if Authentication is disabled.</returns>
        public static async Task<ClaimsIdentity> EnsureValidActivity(IActivity activity, string authHeader, ICredentialProvider credentials, HttpClient httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(authHeader))
            {
                // No auth header was sent. We might be on the anonymous code path. 
                bool isAuthDisabled = await credentials.IsAuthenticationDisabledAsync();
                if (isAuthDisabled)
                {
                    // We are on the anonymous code path. 
                    return new ClaimsIdentity(new List<Claim>(), "anonymous");
                }
            }

            // Go through the standard authentication path. 
            ClaimsIdentity claimsIdentity = await JwtTokenValidation.ValidateAuthHeader(authHeader, credentials, activity.ServiceUrl, httpClient ?? _httpClient);

            // On the standard Auth path, we need to trust the URL that was incoming. 
            MicrosoftAppCredentials.TrustServiceUrl(activity.ServiceUrl);

            return claimsIdentity;
        }

        public static async Task<ClaimsIdentity> ValidateAuthHeader(string authHeader, ICredentialProvider credentials, HttpClient httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(authHeader))
            {
                bool isAuthDisabled = await credentials.IsAuthenticationDisabledAsync();
                if (isAuthDisabled)
                {
                    // In the scenario where Auth is disabled, we still want to have the 
                    // IsAuthenticated flag set in the ClaimsIdentity. To do this requires
                    // adding in an empty claim. 
                    ClaimsIdentity anonymousAuthenticatedIdentity = new ClaimsIdentity(new List<Claim>(), "anonymous");
                    return anonymousAuthenticatedIdentity;
                }

                // No Auth Header. Auth is required. Request is not authorized. 
                throw new UnauthorizedAccessException();
            }

            bool usingEmulator = EmulatorValidation.IsTokenFromEmulator(authHeader);
            if (usingEmulator)
            {
                return await EmulatorValidation.AuthenticateEmulatorToken(authHeader, credentials, httpClient ?? _httpClient);
            }
            else
            {
                return await ChannelValidation.AuthenticateChannelToken(authHeader, credentials, httpClient ?? _httpClient);
            }
        }

        public static async Task<ClaimsIdentity> ValidateAuthHeader(string authHeader, ICredentialProvider credentials, string serviceUrl, HttpClient httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(authHeader))
            {
                bool isAuthDisabled = await credentials.IsAuthenticationDisabledAsync();
                if (isAuthDisabled)
                {
                    // In the scenario where Auth is disabled, we still want to have the 
                    // IsAuthenticated flag set in the ClaimsIdentity. To do this requires
                    // adding in an empty claim. 
                    ClaimsIdentity anonymousAuthenticatedIdentity = new ClaimsIdentity(new List<Claim>(), "anonymous");
                    return anonymousAuthenticatedIdentity;
                }

                // No Auth Header. Auth is required. Request is not authorized. 
                throw new UnauthorizedAccessException();
            }

            bool usingEmulator = EmulatorValidation.IsTokenFromEmulator(authHeader);
            if (usingEmulator)
            {
                return await EmulatorValidation.AuthenticateEmulatorToken(authHeader, credentials, httpClient ?? _httpClient);
            }
            else
            {
                return await ChannelValidation.AuthenticateChannelToken(authHeader, credentials, serviceUrl, httpClient ?? _httpClient);
            }
        }
    }
}
