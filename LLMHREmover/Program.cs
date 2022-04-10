﻿using System;
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

		private static nint _activeHookMouse = 0;
		private static readonly object _activeHookMouseLockObject = new();

		private static nint _activeHookKeyboard = 0;
		private static readonly object _activeHookKeybdLockObject = new();

		private static bool _isUnhooking = false;
		private static readonly object _isUnhookingLockObject = new();

		public static nint ActiveHookMouseHandle
		{
			get
			{
				lock (_activeHookMouseLockObject)
				{
					return _activeHookMouse;
				}
			}
			set
			{
				lock (_activeHookMouseLockObject)
				{
					_activeHookMouse = value;
				}
			}
		}
		public static nint ActiveHookKeybdHandle
		{
			get
			{
				lock (_activeHookKeybdLockObject)
				{
					return _activeHookKeyboard;
				}
			}
			set
			{
				lock (_activeHookKeybdLockObject)
				{
					_activeHookKeyboard = value;
				}
			}
		}
		public static bool IsUnhooking
		{
			get
			{
				lock (_isUnhookingLockObject)
				{
					return _isUnhooking;
				}
			}
			set
			{
				lock (_isUnhookingLockObject)
				{
					_isUnhooking = value;
				}
			}
		}

		static unsafe void Main(string[] args)
		{
			if (!Init()) throw new NullReferenceException($"Failed initializing function pointers");
			else InitializeHooks();

			Console.WriteLine("Pressing enter will close the program ...");
			Console.ReadLine();
		}

		private static void InitializeHooks()
		{
			var process = Process.GetCurrentProcess();
			var module = process.MainModule;
			fixed (char* lpModName = module.ModuleName)
			{
				nint hModule = GetModuleHandle(lpModName);
				Debug.Assert(hModule > 0);

				_hookThread = new Thread(() =>
				{
					lock (_activeHookMouseLockObject)
					{
						if (_activeHookMouse > 0)
						{
							UnhookWindowsHookEx(_activeHookMouse);
							_activeHookMouse = 0;
						}

						_activeHookMouse = SetWindowsHookExMouse(HookType.WH_MOUSE_LL, &LowLevelMouseCallback, hModule, 0);
						Debug.Assert(_activeHookMouse > 0, "SetWindowsHookExMouse returned 0");
						Console.WriteLine("Mouse hook initialized succcessfully!");
					}
					lock (_activeHookKeybdLockObject)
					{
						if (_activeHookKeyboard > 0)
						{
							UnhookWindowsHookEx(_activeHookKeyboard);
							_activeHookKeyboard = 0;
						}

						_activeHookKeyboard = SetWindowsHookExKeyboard(HookType.WH_KEYBOARD_LL, &LowLevelKeyboardCallback, hModule, 0);
						Debug.Assert(_activeHookKeyboard > 0, "SetWindowsHookExKeyboard returned 0");
						Console.WriteLine("Keyboard hook initialized succcessfully!");
					}

					Console.WriteLine("Starting MessagePump");
					MSG msg = default;

					while (GetMessage(&msg, IntPtr.Zero, 0, 0) != -1)
					{
						TranslateMessage(&msg);
						DispatchMessage(&msg);

						if (ActiveHookMouseHandle == 0 && ActiveHookKeybdHandle == 0)
							break;
					}

					Console.WriteLine("GetMessage returned -1 or cancellation was requested, unhooking our windows hook");
					IsUnhooking = true;

					lock (_activeHookMouseLockObject)
					{
						if (_activeHookMouse > 0)
							if (!UnhookWindowsHookEx(_activeHookMouse))
								Console.WriteLine($"UnhookWindowsHookEx with parameter '{_activeHookMouse}' returned false");

						_activeHookMouse = 0;
					}
					lock (_activeHookKeybdLockObject)
					{
						if (_activeHookKeyboard > 0)
							if (!UnhookWindowsHookEx(_activeHookKeyboard))
								Console.WriteLine($"UnhookWindowsHookEx with parameter '{_activeHookKeyboard}' returned false");

						_activeHookKeyboard = 0;
					}
				});
				_hookThread.Start();
			}

			Debug.Assert(_hookThread != null
						 && _hookThread.ThreadState == System.Threading.ThreadState.Running);
		}

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private unsafe static nint LowLevelMouseCallback(int code, IntPtr wParam, MSLLHOOKSTRUCT* lParam)
        {
			if (code < 0 || IsUnhooking) return CallNextHookEx(IntPtr.Zero, code, wParam, (IntPtr)lParam);

			if ((lParam->flags & LLMHF_INJECTED) != 0)
				lParam->flags &= ~LLMHF_INJECTED;

			if ((lParam->flags & LOWER_IL_INJECTED) != 0)
				lParam->flags &= ~LOWER_IL_INJECTED;

			return CallNextHookEx(IntPtr.Zero, code, wParam, (IntPtr)lParam);
		}

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		private unsafe static nint LowLevelKeyboardCallback(int code, IntPtr wParam, KBDLLHOOKSTRUCT* lParam)
		{
			if (code < 0 || IsUnhooking) return CallNextHookEx(IntPtr.Zero, code, wParam, (IntPtr)lParam);

			int keybd_event = (int)wParam;

			if (keybd_event == WM_KEYDOWN 
				|| keybd_event == WM_KEYUP)
			{
				if ((lParam->flags & LLKHF_INJECTED) != 0)
					lParam->flags &= ~LLKHF_INJECTED;

				if ((lParam->flags & LOWER_IL_INJECTED) != 0)
					lParam->flags &= ~LOWER_IL_INJECTED;
			}

			return CallNextHookEx(IntPtr.Zero, code, wParam, (IntPtr)lParam);
		}
	}
}
