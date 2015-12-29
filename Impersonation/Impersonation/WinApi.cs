﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Impersonation
{
   public class WinApi
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            internal Int32 LowPart;
            internal UInt32 HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TOKEN_PRIVILEGES
        {
            internal Int32 PrivilegeCount;
            internal LUID Luid;
            internal Int32 Attributes;
        }

        public const int SE_PRIVILEGE_ENABLED = 0x00000002;

        public const int ERROR_NOT_ALL_ASSIGNED = 1300;

        public enum SecurityEntity
        {
            SE_CREATE_TOKEN_NAME,
            SE_ASSIGNPRIMARYTOKEN_NAME,
            SE_LOCK_MEMORY_NAME,
            SE_INCREASE_QUOTA_NAME,
            SE_UNSOLICITED_INPUT_NAME,
            SE_MACHINE_ACCOUNT_NAME,
            SE_TCB_NAME,
            SE_SECURITY_NAME,
            SE_TAKE_OWNERSHIP_NAME,
            SE_LOAD_DRIVER_NAME,
            SE_SYSTEM_PROFILE_NAME,
            SE_SYSTEMTIME_NAME,
            SE_PROF_SINGLE_PROCESS_NAME,
            SE_INC_BASE_PRIORITY_NAME,
            SE_CREATE_PAGEFILE_NAME,
            SE_CREATE_PERMANENT_NAME,
            SE_BACKUP_NAME,
            SE_RESTORE_NAME,
            SE_SHUTDOWN_NAME,
            SE_DEBUG_NAME,
            SE_AUDIT_NAME,
            SE_SYSTEM_ENVIRONMENT_NAME,
            SE_CHANGE_NOTIFY_NAME,
            SE_REMOTE_SHUTDOWN_NAME,
            SE_UNDOCK_NAME,
            SE_SYNC_AGENT_NAME,
            SE_ENABLE_DELEGATION_NAME,
            SE_MANAGE_VOLUME_NAME,
            SE_IMPERSONATE_NAME,
            SE_CREATE_GLOBAL_NAME,
            SE_CREATE_SYMBOLIC_LINK_NAME,
            SE_INC_WORKING_SET_NAME,
            SE_RELABEL_NAME,
            SE_TIME_ZONE_NAME,
            SE_TRUSTED_CREDMAN_ACCESS_NAME
        }

       [Flags]
       public enum TokenFlags : uint
       {
           STANDARD_RIGHTS_REQUIRED = 0x000F0000,
           STANDARD_RIGHTS_READ = 0x00020000,
           TOKEN_ASSIGN_PRIMARY = 0x0001,
           TOKEN_DUPLICATE = 0x0002,
           TOKEN_IMPERSONATE = 0x0004,
           TOKEN_QUERY = 0x0008,
           TOKEN_QUERY_SOURCE = 0x0010,
           TOKEN_ADJUST_PRIVILEGES = 0x0020,
           TOKEN_ADJUST_GROUPS = 0x0040,
           TOKEN_ADJUST_DEFAULT = 0x0080,
           TOKEN_ADJUST_SESSIONID = 0x0100,
           TOKEN_READ = (STANDARD_RIGHTS_READ | TOKEN_QUERY),
           TOKEN_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED | TOKEN_ASSIGN_PRIMARY |
              TOKEN_DUPLICATE | TOKEN_IMPERSONATE | TOKEN_QUERY | TOKEN_QUERY_SOURCE |
              TOKEN_ADJUST_PRIVILEGES | TOKEN_ADJUST_GROUPS | TOKEN_ADJUST_DEFAULT |
              TOKEN_ADJUST_SESSIONID)
       }

       [Flags]
       public enum ProcessAccessFlags : uint
       {
           All = 0x001F0FFF,
           Terminate = 0x00000001,
           CreateThread = 0x00000002,
           VirtualMemoryOperation = 0x00000008,
           VirtualMemoryRead = 0x00000010,
           VirtualMemoryWrite = 0x00000020,
           DuplicateHandle = 0x00000040,
           CreateProcess = 0x000000080,
           SetQuota = 0x00000100,
           SetInformation = 0x00000200,
           QueryInformation = 0x00000400,
           QueryLimitedInformation = 0x00001000,
           Synchronize = 0x00100000
       }

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(
             ProcessAccessFlags processAccess,
             bool bInheritHandle,
             int processId
        );

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool OpenProcessToken(IntPtr ProcessHandle,
            UInt32 DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool LookupPrivilegeValue(string lpsystemname, string lpname, [MarshalAs(UnmanagedType.Struct)] ref LUID lpLuid);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AdjustTokenPrivileges(IntPtr tokenhandle,
                                 [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges,
                                 [MarshalAs(UnmanagedType.Struct)]ref TOKEN_PRIVILEGES newstate,
                                 uint bufferlength, IntPtr previousState, IntPtr returnlength);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern Boolean CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr GetCurrentProcess();


        public static IntPtr OpenProcess(Process proc, ProcessAccessFlags flags)
        {
            return OpenProcess(flags, false, proc.Id);
        }

        public static IntPtr OpenProcess(int pid, ProcessAccessFlags flags)
        {
            return OpenProcess(flags, false, pid);
        }

        public static IntPtr GetUserTokenFromProcessId(int pid)
        {
            int lastError = 0;
            
            EnablePrivilege(SecurityEntity.SE_ASSIGNPRIMARYTOKEN_NAME);
            EnablePrivilege(SecurityEntity.SE_INCREASE_QUOTA_NAME);

            IntPtr processToken = OpenProcess(pid, ProcessAccessFlags.All);
            if ((lastError = Marshal.GetLastWin32Error()) != 0)
            {
                throw new ApplicationException(String.Format("OpenProcess Failed. Errorcode {0}", lastError));
            }

           IntPtr userToken = IntPtr.Zero;
           bool success = OpenProcessToken(processToken, (uint)(TokenFlags.TOKEN_DUPLICATE | TokenFlags.TOKEN_IMPERSONATE | TokenFlags.TOKEN_QUERY), out userToken);
           if ((lastError = Marshal.GetLastWin32Error()) != 0)
           {
               throw new ApplicationException(String.Format("OpenProcess Failed. Errorcode {0}", lastError));
           }
           return userToken;
        }

        public static void EnablePrivilege(SecurityEntity securityEntity)
        {
            if (!Enum.IsDefined(typeof(SecurityEntity), securityEntity))
                throw new InvalidEnumArgumentException("securityEntity", (int)securityEntity, typeof(SecurityEntity));

            var securityEntityValue = GetSecurityEntityValue(securityEntity);
            try
            {
                var locallyUniqueIdentifier = new LUID();

                if (LookupPrivilegeValue(null, securityEntityValue, ref locallyUniqueIdentifier))
                {
                    var TOKEN_PRIVILEGES = new TOKEN_PRIVILEGES();
                    TOKEN_PRIVILEGES.PrivilegeCount = 1;
                    TOKEN_PRIVILEGES.Attributes = SE_PRIVILEGE_ENABLED;
                    TOKEN_PRIVILEGES.Luid = locallyUniqueIdentifier;

                    var tokenHandle = IntPtr.Zero;
                    try
                    {
                        var currentProcess = GetCurrentProcess();
                        if (OpenProcessToken(currentProcess, (uint)(TokenFlags.TOKEN_ADJUST_PRIVILEGES | TokenFlags.TOKEN_QUERY), out tokenHandle))
                        {
                            if (AdjustTokenPrivileges(tokenHandle, false,
                                                ref TOKEN_PRIVILEGES,
               1024, IntPtr.Zero, IntPtr.Zero))
                            {
                                var lastError = Marshal.GetLastWin32Error();
                                if (lastError == ERROR_NOT_ALL_ASSIGNED)
                                {
                                    var win32Exception = new Win32Exception();
                                    throw new InvalidOperationException("AdjustTokenPrivileges failed.", win32Exception);
                                }
                            }
                            else
                            {
                                var win32Exception = new Win32Exception();
                                throw new InvalidOperationException("AdjustTokenPrivileges failed.", win32Exception);
                            }
                        }
                        else
                        {
                            var win32Exception = new Win32Exception();

                            var exceptionMessage = string.Format(CultureInfo.InvariantCulture,
                                                "OpenProcessToken failed. CurrentProcess: {0}",
                                                currentProcess.ToInt32());

                            throw new InvalidOperationException(exceptionMessage, win32Exception);
                        }
                    }
                    finally
                    {
                        if (tokenHandle != IntPtr.Zero)
                            CloseHandle(tokenHandle);
                    }
                }
                else
                {
                    var win32Exception = new Win32Exception();

                    var exceptionMessage = string.Format(CultureInfo.InvariantCulture,
                                        "LookupPrivilegeValue failed. SecurityEntityValue: {0}",
                                        securityEntityValue);

                    throw new InvalidOperationException(exceptionMessage, win32Exception);
                }
            }
            catch (Exception e)
            {
                var exceptionMessage = string.Format(CultureInfo.InvariantCulture,
                                 "GrandPrivilege failed. SecurityEntity: {0}. {1}",
                                 securityEntity, e.Message);

                throw new InvalidOperationException(exceptionMessage, e);
            }
        }

        private static string GetSecurityEntityValue(SecurityEntity securityEntity)
        {
            switch (securityEntity)
            {
                case SecurityEntity.SE_ASSIGNPRIMARYTOKEN_NAME:
                    return "SeAssignPrimaryTokenPrivilege";
                case SecurityEntity.SE_AUDIT_NAME:
                    return "SeAuditPrivilege";
                case SecurityEntity.SE_BACKUP_NAME:
                    return "SeBackupPrivilege";
                case SecurityEntity.SE_CHANGE_NOTIFY_NAME:
                    return "SeChangeNotifyPrivilege";
                case SecurityEntity.SE_CREATE_GLOBAL_NAME:
                    return "SeCreateGlobalPrivilege";
                case SecurityEntity.SE_CREATE_PAGEFILE_NAME:
                    return "SeCreatePagefilePrivilege";
                case SecurityEntity.SE_CREATE_PERMANENT_NAME:
                    return "SeCreatePermanentPrivilege";
                case SecurityEntity.SE_CREATE_SYMBOLIC_LINK_NAME:
                    return "SeCreateSymbolicLinkPrivilege";
                case SecurityEntity.SE_CREATE_TOKEN_NAME:
                    return "SeCreateTokenPrivilege";
                case SecurityEntity.SE_DEBUG_NAME:
                    return "SeDebugPrivilege";
                case SecurityEntity.SE_ENABLE_DELEGATION_NAME:
                    return "SeEnableDelegationPrivilege";
                case SecurityEntity.SE_IMPERSONATE_NAME:
                    return "SeImpersonatePrivilege";
                case SecurityEntity.SE_INC_BASE_PRIORITY_NAME:
                    return "SeIncreaseBasePriorityPrivilege";
                case SecurityEntity.SE_INCREASE_QUOTA_NAME:
                    return "SeIncreaseQuotaPrivilege";
                case SecurityEntity.SE_INC_WORKING_SET_NAME:
                    return "SeIncreaseWorkingSetPrivilege";
                case SecurityEntity.SE_LOAD_DRIVER_NAME:
                    return "SeLoadDriverPrivilege";
                case SecurityEntity.SE_LOCK_MEMORY_NAME:
                    return "SeLockMemoryPrivilege";
                case SecurityEntity.SE_MACHINE_ACCOUNT_NAME:
                    return "SeMachineAccountPrivilege";
                case SecurityEntity.SE_MANAGE_VOLUME_NAME:
                    return "SeManageVolumePrivilege";
                case SecurityEntity.SE_PROF_SINGLE_PROCESS_NAME:
                    return "SeProfileSingleProcessPrivilege";
                case SecurityEntity.SE_RELABEL_NAME:
                    return "SeRelabelPrivilege";
                case SecurityEntity.SE_REMOTE_SHUTDOWN_NAME:
                    return "SeRemoteShutdownPrivilege";
                case SecurityEntity.SE_RESTORE_NAME:
                    return "SeRestorePrivilege";
                case SecurityEntity.SE_SECURITY_NAME:
                    return "SeSecurityPrivilege";
                case SecurityEntity.SE_SHUTDOWN_NAME:
                    return "SeShutdownPrivilege";
                case SecurityEntity.SE_SYNC_AGENT_NAME:
                    return "SeSyncAgentPrivilege";
                case SecurityEntity.SE_SYSTEM_ENVIRONMENT_NAME:
                    return "SeSystemEnvironmentPrivilege";
                case SecurityEntity.SE_SYSTEM_PROFILE_NAME:
                    return "SeSystemProfilePrivilege";
                case SecurityEntity.SE_SYSTEMTIME_NAME:
                    return "SeSystemtimePrivilege";
                case SecurityEntity.SE_TAKE_OWNERSHIP_NAME:
                    return "SeTakeOwnershipPrivilege";
                case SecurityEntity.SE_TCB_NAME:
                    return "SeTcbPrivilege";
                case SecurityEntity.SE_TIME_ZONE_NAME:
                    return "SeTimeZonePrivilege";
                case SecurityEntity.SE_TRUSTED_CREDMAN_ACCESS_NAME:
                    return "SeTrustedCredManAccessPrivilege";
                case SecurityEntity.SE_UNDOCK_NAME:
                    return "SeUndockPrivilege";
                default:
                    throw new ArgumentOutOfRangeException(typeof(SecurityEntity).Name);
            }
        }
    }
}
