using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static LLMHREmover.Enums;
using static LLMHREmover.Structs;

namespace LLMHREmover
{
	unsafe class Imports
	{
		public static delegate* unmanaged[Stdcall]<HookType, // hookType
												   delegate* unmanaged[Stdcall]<int, nint, MSLLHOOKSTRUCT*, nint>,  // lpFn, managed: (int code, IntPtr wParam, IntPtr lParam)
												   nint, // hMod
												   uint, // dwThreadIt
												   nint  /* Return Type */> SetWindowsHookExMouse = null;

		public static delegate* unmanaged[Stdcall]<HookType, // hookType
												   delegate* unmanaged[Stdcall]<int, nint, KBDLLHOOKSTRUCT*, nint>,  // lpFn, managed: (int code, IntPtr wParam, IntPtr lParam)
												   nint, // hMod
												   uint, // dwThreadIt
												   nint  /* Return Type */> SetWindowsHookExKeyboard = null;

		public static delegate* unmanaged[Stdcall]<nint, // hkk
												   bool  /* Return Type */> UnhookWindowsHookEx = null;


		public static delegate* unmanaged[Stdcall]<nint, // hkk
												   int,  // nCode
												   nint, // wParam
												   IntPtr, // lParam
												   nint  /* Return Type */> CallNextHookEx = null;

		public static delegate* unmanaged[Stdcall]<char*, // module name
												   nint   /* Return Type */> GetModuleHandle = null;

		public static delegate* unmanaged[Stdcall]<MSG*, 
												   nint, // hWnd 
												   uint, // wMsgFilterMin
												   uint, // wMsgFilterMax
												   int   /* Return Type */> GetMessage;

		public static delegate* unmanaged[Stdcall]<MSG*, bool> TranslateMessage;
		public static delegate* unmanaged[Stdcall]<MSG*, nint> DispatchMessage;

		public static bool Init()
		{
			nint user32dll = NativeLibrary.Load("user32.dll");
			nint kernel32dll = NativeLibrary.Load("kernel32.dll");

			Debug.Assert(user32dll > 0);
			Debug.Assert(kernel32dll > 0);

			SetWindowsHookExMouse = (delegate* unmanaged[Stdcall]<HookType, delegate* unmanaged[Stdcall]<int, nint, MSLLHOOKSTRUCT*, nint>, nint, uint, nint>)NativeLibrary.GetExport(user32dll, "SetWindowsHookExA");
			SetWindowsHookExKeyboard = (delegate* unmanaged[Stdcall]<HookType, delegate* unmanaged[Stdcall]<int, nint, KBDLLHOOKSTRUCT*, nint>, nint, uint, nint>)NativeLibrary.GetExport(user32dll, "SetWindowsHookExA");
			UnhookWindowsHookEx = (delegate* unmanaged[Stdcall]<nint, bool>)NativeLibrary.GetExport(user32dll, "UnhookWindowsHookEx");
			CallNextHookEx = (delegate* unmanaged[Stdcall]<nint, int, nint, IntPtr, nint>)NativeLibrary.GetExport(user32dll, "CallNextHookEx");
			GetModuleHandle = (delegate* unmanaged[Stdcall]<char*, nint>)NativeLibrary.GetExport(kernel32dll, "GetModuleHandleW");

			GetMessage = (delegate* unmanaged[Stdcall]<MSG*, nint, uint, uint, int>)NativeLibrary.GetExport(user32dll, "GetMessageW");
			TranslateMessage = (delegate* unmanaged[Stdcall]<MSG*, bool>)NativeLibrary.GetExport(user32dll, "TranslateMessage");
			DispatchMessage = (delegate* unmanaged[Stdcall]<MSG*, nint>)NativeLibrary.GetExport(user32dll, "DispatchMessageW");

			//NativeLibrary.Free(user32dll);
			//NativeLibrary.Free(kernel32dll);

			Debug.Assert(SetWindowsHookExMouse != null);
			Debug.Assert(SetWindowsHookExKeyboard != null);
			Debug.Assert(UnhookWindowsHookEx != null);
			Debug.Assert(CallNextHookEx   != null);
			Debug.Assert(GetModuleHandle  != null);

			Debug.Assert(GetMessage != null);
			Debug.Assert(TranslateMessage != null);
			Debug.Assert(DispatchMessage != null);

			return SetWindowsHookExMouse != null
				   && SetWindowsHookExKeyboard != null
				   && UnhookWindowsHookEx != null
				   && CallNextHookEx != null
				   && GetModuleHandle != null
				   && GetMessage != null
				   && TranslateMessage != null
				   && DispatchMessage != null;
		}
	}

	public class Enums
	{
		public enum HookType : int
		{
			WH_JOURNALRECORD = 0,
			WH_JOURNALPLAYBACK = 1,
			WH_KEYBOARD = 2,
			WH_GETMESSAGE = 3,
			WH_CALLWNDPROC = 4,
			WH_CBT = 5,
			WH_SYSMSGFILTER = 6,
			WH_MOUSE = 7,
			WH_HARDWARE = 8,
			WH_DEBUG = 9,
			WH_SHELL = 10,
			WH_FOREGROUNDIDLE = 11,
			WH_CALLWNDPROCRET = 12,
			WH_KEYBOARD_LL = 13,
			WH_MOUSE_LL = 14
		}
	}

	public class Structs
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct POINT
		{
			public int X;
			public int Y;

			public POINT(int x, int y)
			{
				this.X = x;
				this.Y = y;
			}

			public static implicit operator System.Drawing.Point(POINT p)
			{
				return new System.Drawing.Point(p.X, p.Y);
			}

			public static implicit operator POINT(System.Drawing.Point p)
			{
				return new POINT(p.X, p.Y);
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct MSG
		{
			IntPtr hwnd;
			uint message;
			UIntPtr wParam;
			IntPtr lParam;
			int time;
			POINT pt;
			int lPrivate;
		}

		[StructLayout(LayoutKind.Sequential)]
		public unsafe struct MSLLHOOKSTRUCT
		{
			public POINT pt;
			public nint mouseData;
			public nint flags;
			public nint time;

			// ULONG_PTR dwExtraInfo;
			public nuint* dwExtraInfo;
		}

		[StructLayout(LayoutKind.Sequential)]
		public unsafe struct KBDLLHOOKSTRUCT
		{
			public uint vkCode;
			public uint scanCode;
			public nint flags;
			public uint time;
			public nuint* dwExtraInfo;
		}
	}

	public class Constants
	{
		public const int LLMHF_INJECTED = 0x00000001;
		public const int LLKHF_INJECTED = 0x00000010;

		public const int LOWER_IL_INJECTED = 0x00000002;

		public const int WM_KEYDOWN = 0x0100;
		public const int WM_KEYUP = 0x0101;
	}
}
