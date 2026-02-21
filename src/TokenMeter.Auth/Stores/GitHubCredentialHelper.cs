using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TokenMeter.Auth.Stores;

public static class GitHubCredentialHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("Advapi32.dll", SetLastError = true, EntryPoint = "CredReadW", CharSet = CharSet.Unicode)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("Advapi32.dll", SetLastError = true, EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr credentialPtr);

    private const int CRED_TYPE_GENERIC = 1;

    public static string? ReadGitHubToken()
    {
        string[] targets = { "codexbar-copilot", "git:https://github.com", "github.com" };

        foreach (var target in targets)
        {
            if (CredRead(target, CRED_TYPE_GENERIC, 0, out var ptr))
            {
                try
                {
                    var credential = Marshal.PtrToStructure<CREDENTIAL>(ptr);
                    if (credential.CredentialBlobSize > 0 && credential.CredentialBlob != IntPtr.Zero)
                    {
                        var blob = new byte[credential.CredentialBlobSize];
                        Marshal.Copy(credential.CredentialBlob, blob, 0, credential.CredentialBlobSize);
                        var token = Encoding.UTF8.GetString(blob).Trim('\0');

                        // Handle username:token format from Git Credential Manager
                        if (token.Contains(':'))
                        {
                            var parts = token.Split(':', 2);
                            return parts[1].Trim();
                        }

                        return token.Trim();
                    }
                }
                finally
                {
                    CredFree(ptr);
                }
            }
        }

        return null;
    }
}
