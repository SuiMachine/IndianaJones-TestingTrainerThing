using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Flying47
{
    public enum codeInjectionResult
    {
        Success,
        Failure,
        ProcessNotFound,
        FailedToOpenProcess,
        FailedToGetProcAddress,
        FailedToVirtualAlloc,
        FailedToWriteInstructionToMemory
    }




    class codeInjection
    {
        #region DLLImports
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, int bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] buffer, uint size, int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] buffer, uint size, ref int lpNumberOfBytesToRead);
        #endregion

        static readonly IntPtr INTPTR_ZERO = (IntPtr)0;
        public codeInjectionResult result;
        public Process process;
        public IntPtr alocAdress;
        private uint injectedCodeLenght;
        public IntPtr returnToAdress;           //addressToHook + lenght
        public IntPtr locationOfReturnJmp;      //alocAdress + alocLenght
        public IntPtr addressToHook;
        byte[] originalCode;
        byte[] modifiedCode;


        public codeInjection(Process process, uint addressToHook, byte instrLenghtAtHook, byte[] injectedCodeAsBytes, bool autohook = true)
        {
            this.process = process;
            this.addressToHook = (IntPtr)addressToHook;
            this.returnToAdress = (IntPtr)(addressToHook + instrLenghtAtHook);

            result = Inject(injectedCodeAsBytes, instrLenghtAtHook);
            EnableHook();
        }

        public bool updateJmpInstructions(uint adressToHook, byte instrLenghtAtHook)
        {
            modifiedCode = prepareHOOKJMP((IntPtr)addressToHook, instrLenghtAtHook, alocAdress);
            uint pID = (uint)process.Id;
            IntPtr procHandle = OpenProcess((0x2 | 0x8 | 0x10 | 0x20 | 0x400), 1, pID);
            if (procHandle == INTPTR_ZERO)
            {
                return false;
            }

            IntPtr lpLLAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
            if (lpLLAddress == INTPTR_ZERO)
            {
                return false;
            }

            byte[] backAdr = BitConverter.GetBytes((uint)returnToAdress + instrLenghtAtHook - ((uint)alocAdress + injectedCodeLenght + 5));
            if (WriteProcessMemory(procHandle, locationOfReturnJmp, backAdr, 4, 0) == 0)
            {
                return false;
            }

            CloseHandle(procHandle);
            return true;
        }

        public bool EnableHook()
        {
            uint pID = (uint)process.Id;
            IntPtr procHandle = OpenProcess((0x2 | 0x8 | 0x10 | 0x20 | 0x400), 1, pID);
            if (procHandle == INTPTR_ZERO)
            {
                return false;
            }

            if (WriteProcessMemory(procHandle, addressToHook, modifiedCode, (uint)modifiedCode.Length, 0) == 0)
            {
                return false;
            }

            CloseHandle(procHandle);
            return true;
        }

        public bool DisableHook()
        {
            uint pID = (uint)process.Id;
            IntPtr procHandle = OpenProcess((0x2 | 0x8 | 0x10 | 0x20 | 0x400), 1, pID);
            if (procHandle == INTPTR_ZERO)
            {
                return false;
            }

            if (WriteProcessMemory(procHandle, addressToHook, originalCode, (uint)modifiedCode.Length, 0) == 0)
            {
                return false;
            }

            CloseHandle(procHandle);
            return true;
        }

        private codeInjectionResult Inject(byte[] injectedCodeAsBytes, byte instrLenghtAtHook)
        {
            if(process.Id == 0)
            {
                return codeInjectionResult.ProcessNotFound;
            }

            return bInject(injectedCodeAsBytes, instrLenghtAtHook);
        }

        private codeInjectionResult bInject(byte[] injectedCodeAsBytes, byte instrLenghtAtHook)
        {
            uint pID = (uint)process.Id;

            IntPtr procHandle = OpenProcess((0x2 | 0x8 | 0x10 | 0x20 | 0x400), 1, pID);
            if(procHandle == INTPTR_ZERO)
            {
                return codeInjectionResult.FailedToOpenProcess;
            }

            IntPtr lpLLAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
            if(lpLLAddress == INTPTR_ZERO)
            {
                return codeInjectionResult.FailedToGetProcAddress;
            }

            uint lenght = (uint)injectedCodeAsBytes.Length + 6;         //because we inject code and we plan for return jmp

            byte[] fullInjectedCode = new byte[lenght];
            injectedCodeAsBytes.CopyTo(fullInjectedCode, 0);
            fullInjectedCode[lenght - 6] = 0xE9;                        //Set the jump
            injectedCodeLenght = (uint)fullInjectedCode.Length;

            IntPtr lpAddress = VirtualAllocEx(procHandle, (IntPtr)null, (IntPtr)512, (0x1000 | 0x2000), 0X40);      //could try taking alloc less memory?
            alocAdress = lpAddress;
            locationOfReturnJmp = (IntPtr)((uint)alocAdress + injectedCodeLenght - 5);

            if (lpAddress == INTPTR_ZERO)
            {
                return codeInjectionResult.FailedToVirtualAlloc;
            }

            modifiedCode = prepareHOOKJMP(addressToHook, instrLenghtAtHook, alocAdress);

            if (WriteProcessMemory(procHandle, lpAddress, fullInjectedCode, (uint)fullInjectedCode.Length, 0) == 0)
            {
                return codeInjectionResult.FailedToWriteInstructionToMemory;
            }

            int bytesRead = 0;
            originalCode = new byte[instrLenghtAtHook];
            ReadProcessMemory(procHandle, addressToHook, originalCode, (uint)originalCode.Length, ref bytesRead);

            uint adressForJump = (uint)alocAdress + (uint)injectedCodeLenght - 5;
            byte[] backAdr = BitConverter.GetBytes((uint)returnToAdress + instrLenghtAtHook - ((uint)alocAdress + injectedCodeLenght + 5));
            if (WriteProcessMemory(procHandle, locationOfReturnJmp, backAdr, 4, 0) == 0)
            {
                return codeInjectionResult.FailedToWriteInstructionToMemory;
            }

            CloseHandle(procHandle);
            
            return codeInjectionResult.Success;
        }

        private byte[] prepareHOOKJMP(IntPtr addressToHook, byte instrLenghtAtHook, IntPtr allocatedAdress)
        {
            List<byte> opCodes = new List<byte>();
            opCodes.Add(0xE9);
            foreach (byte opByte in BitConverter.GetBytes((uint)(allocatedAdress.ToInt32() - addressToHook.ToInt32())))
            {
                opCodes.Add(opByte);
            }
            for(int i=5; i<instrLenghtAtHook; i++)
            {
                opCodes.Add(0x90);
            }

            return opCodes.ToArray();
        }

        #region StaticStuffToHelp
        public static byte[] stringBytesToArray(string bytesAsString)
        {
            bytesAsString = bytesAsString.Replace(" ", "");
            
            if (bytesAsString.Length % 2 != 0)
            {
                throw new Exception("Provided string to conversion does not contain proper amount of bits.");
            }

            byte[] outArray = new byte[bytesAsString.Length / 2];
            for (int i = 0; i < outArray.Length; i++)
            {
                byte temp = (byte)(CharToByte(bytesAsString[i * 2])*16 + CharToByte(bytesAsString[i * 2 +1]));
                outArray[i] = temp;
            }
            return outArray;
        }

        private static byte CharToByte(char c)
        {
            c = char.ToLower(c);
            switch (c)
            {
                case '0': return 0;
                case '1': return 1;
                case '2': return 2;
                case '3': return 3;
                case '4': return 4;
                case '5': return 5;
                case '6': return 6;
                case '7': return 7;
                case '8': return 8;
                case '9': return 9;
                case 'a': return 10;
                case 'b': return 11;
                case 'c': return 12;
                case 'd': return 13;
                case 'e': return 14;
                case 'f': return 15;
            }
            throw new FormatException("Invalid char in a string");
        }
        #endregion
    }
}
