using System;
using System.Diagnostics;
using System.Threading;

using static LLMHREmover.Imports;
using static LLMHREmover.Enums;
using static LLMHREmover.Structs;
using static LLMHREmover.Constants;

using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;


namespace LLMHREmover
{
	unsafe class Program
	{
		private static Thread _hookThread = null;

		private static nint _activeHook = 0;
		private static readonly object _activeHookLockObject = new();

		public static nint ActiveHookHandle
		{
			get
			{
				lock (_activeHookLockObject)
				{
					return _activeHook;
				}
			}
		}

		static unsafe void Main(string[] args)
		{
			if (!Init()) throw new NullReferenceException($"Failed initializing function pointers");

			using Process process = Process.GetCurrentProcess();
			using ProcessModule module = process.MainModule;

			fixed (char* lpModName = module.ModuleName)
			{
				nint hModule = GetModuleHandle(lpModName);
				Debug.Assert(hModule > 0);

				_hookThread = new Thread(() =>
				{ 
					lock (_activeHookLockObject)
					{
						_activeHook = SetWindowsHookEx(HookType.WH_MOUSE_LL, &LowLevelMouseCallback, hModule, 0);
						Debug.Assert(_activeHook > 0, "SetWindowsHookEx returned 0");
					}
					
					Console.WriteLine("Starting MessagePump");
					MSG msg = default;

					while (GetMessage(&msg, IntPtr.Zero, 0, 0) != -1)
					{
						TranslateMessage(&msg);
						DispatchMessage(&msg);
					}

					Console.WriteLine("GetMessage returned -1, unhooking our windows hook");
					if (!UnhookWindowsHookEx(_activeHook)) 
						Console.WriteLine($"UnhookWindowsHookEx with parameter '{_activeHook}' returned false");

					_activeHook = 0;

					lock (_activeHookLockObject)
					{
						_activeHook = 0;
					}
				});
				_hookThread.Start();
			}

			Debug.Assert(_hookThread != null
						 && _hookThread.ThreadState == System.Threading.ThreadState.Running);

			Console.WriteLine("Pressing enter will close the program ...");
			Console.ReadLine();
		}

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private unsafe static nint LowLevelMouseCallback(int code, IntPtr wParam, MSLLHOOKSTRUCT* lParam)
        {
			if (code < 0) return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);

			// Can filter out only mouse movements from your application only by
			// setting mInfo->dwExtraInfo to some constant value when calling SendInput/mouse_event
			// in some other program
			// and then just checking for that constant value here before doing any stripping

			if ((lParam->flags & LLMHF_INJECTED) != 0)
				lParam->flags &= ~LLMHF_INJECTED;

			if ((lParam->flags & LLMHF_LOWER_IL_INJECTED) != 0)
				lParam->flags &= ~LLMHF_LOWER_IL_INJECTED;

			return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }
    }
}
