using System.Runtime.InteropServices;
using System.Text;

namespace Murmur.Services;

/// <summary>
/// API keys in the Windows Credential Manager ("Windows Credentials", generic): per user,
/// encrypted by Windows, never in a file or the log.
/// </summary>
public static partial class CredentialStore
{
    private const string Category = "credentials";
    private const uint GenericType = 1;
    // stays on this pc for this user, doesn't roam with the profile
    private const uint PersistLocalMachine = 2;
    private const int NotFound = 1168;

    public static string? Read(string target)
    {
        unsafe
        {
            if (!NativeMethods.CredReadW(target, GenericType, 0, out var credential))
            {
                LogFailure("read", Marshal.GetLastPInvokeError());
                return null;
            }
            try
            {
                var blob = new ReadOnlySpan<byte>(credential->CredentialBlob, (int)credential->CredentialBlobSize);
                var value = Encoding.Unicode.GetString(blob);
                return value.Length == 0 ? null : value;
            }
            finally
            {
                NativeMethods.CredFree(credential);
            }
        }
    }

    public static bool Contains(string target) => Read(target) is not null;

    /// <summary>Saves the value; an empty value deletes the credential. Returns whether it worked.</summary>
    public static bool Write(string target, string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return Delete(target);
        }
        var blob = Encoding.Unicode.GetBytes(trimmed);
        unsafe
        {
            fixed (char* targetName = target)
            fixed (char* userName = Environment.UserName)
            fixed (byte* blobPointer = blob)
            {
                var credential = new NativeMethods.Credential
                {
                    Type = GenericType,
                    TargetName = targetName,
                    CredentialBlobSize = (uint)blob.Length,
                    CredentialBlob = blobPointer,
                    Persist = PersistLocalMachine,
                    UserName = userName,
                };
                if (NativeMethods.CredWriteW(&credential, 0))
                {
                    return true;
                }
            }
        }
        LogFailure("write", Marshal.GetLastPInvokeError());
        return false;
    }

    public static bool Delete(string target)
    {
        if (NativeMethods.CredDeleteW(target, GenericType, 0))
        {
            return true;
        }
        var error = Marshal.GetLastPInvokeError();
        LogFailure("delete", error);
        return error == NotFound;
    }

    private static void LogFailure(string operation, int error)
    {
        // a missing key is normal (not set up yet), anything else is worth a line
        if (error != NotFound)
        {
            Log.Error(Category, $"{operation} failed with win32 error {error}");
        }
    }

    private static unsafe partial class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Credential
        {
            public uint Flags;
            public uint Type;
            public char* TargetName;
            public char* Comment;
            public long LastWritten;
            public uint CredentialBlobSize;
            public byte* CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public nint Attributes;
            public char* TargetAlias;
            public char* UserName;
        }

        [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CredReadW(string target, uint type, uint flags, out Credential* credential);

        [LibraryImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CredWriteW(Credential* credential, uint flags);

        [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CredDeleteW(string target, uint type, uint flags);

        [LibraryImport("advapi32.dll")]
        public static partial void CredFree(void* buffer);
    }
}
