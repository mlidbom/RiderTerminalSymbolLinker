package dev.magnus.claudesessions.windows

import com.sun.jna.Native
import com.sun.jna.Pointer
import com.sun.jna.platform.win32.WinDef
import java.awt.Window

/**
 * A window's native handle, as Windows names it, its `HWND`: other tools find the window by it, as the hub does to tell
 * whether it's the window in front.
 */
@JvmInline
value class WindowHandle(val value: Long) {
    /** The handle as the Windows API takes it. */
    val hwnd: WinDef.HWND get() = WinDef.HWND(Pointer(value))

    companion object {
        /** The handle of [window], which is shown. */
        fun of(window: Window): WindowHandle = WindowHandle(Pointer.nativeValue(Native.getWindowPointer(window)))
    }
}
