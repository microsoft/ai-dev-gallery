// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Security.Credentials;

namespace AIDevGallery.Utils;

internal static class CredentialManager
{
    private static string GetTargetName(string name)
    {
        return $"AIDevGallery:name={name}";
    }

    public static unsafe string? ReadCredential(string name)
    {
        var result = PInvoke.CredRead(GetTargetName(name), CRED_TYPE.CRED_TYPE_GENERIC, out var cred);
        if (result)
        {
            try
            {
                string? secret = null;
                if (cred->CredentialBlob is not null)
                {
                    secret = Marshal.PtrToStringUni((nint)cred->CredentialBlob, (int)(cred->CredentialBlobSize / UnicodeEncoding.CharSize));
                }

                return secret;
            }
            finally
            {
                PInvoke.CredFree(cred);
            }
        }

        return null;
    }

    public static unsafe bool WriteCredential(string name, string secret)
    {
        fixed (char* targetNamePtr = GetTargetName(name))
        {
            fixed (char* namePtrPtr = name)
            {
                fixed (char* secretPtr = secret)
                {
                    var cred = new CREDENTIALW
                    {
                        Type = CRED_TYPE.CRED_TYPE_GENERIC,
                        TargetName = targetNamePtr,
                        CredentialBlobSize = (uint)(secret.Length * UnicodeEncoding.CharSize),
                        CredentialBlob = (byte*)secretPtr,
                        UserName = namePtrPtr,
                        Persist = CRED_PERSIST.CRED_PERSIST_LOCAL_MACHINE,
                        AttributeCount = 0u,
                        Attributes = null,
                        Comment = null,
                        TargetAlias = default,
                    };

                    return PInvoke.CredWrite(in cred, 0);
                }
            }
        }
    }

    public static unsafe bool DeleteCredential(string name)
    {
        return PInvoke.CredDelete(GetTargetName(name), CRED_TYPE.CRED_TYPE_GENERIC);
    }
}