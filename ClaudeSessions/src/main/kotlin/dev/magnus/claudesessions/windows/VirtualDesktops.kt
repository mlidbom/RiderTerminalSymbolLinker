package dev.magnus.claudesessions.windows

import com.intellij.openapi.util.SystemInfo
import com.sun.jna.Pointer
import com.sun.jna.platform.win32.COM.COMUtils
import com.sun.jna.platform.win32.COM.Unknown
import com.sun.jna.platform.win32.Guid
import com.sun.jna.platform.win32.Ole32
import com.sun.jna.platform.win32.WTypes
import com.sun.jna.platform.win32.WinDef
import com.sun.jna.ptr.PointerByReference
import java.util.UUID
import java.util.concurrent.Callable
import java.util.concurrent.Executors

/**
 * Windows' virtual desktops, through its documented `IVirtualDesktopManager`: which desktop a window is on, and moving
 * one of this process's own windows to another, as Windows lets a process move only its own.
 *
 * The COM object is made and called on a thread of its own, which joined COM's multithreaded apartment, as COM requires
 * of a thread that calls it. Elsewhere than on Windows there are no virtual desktops to tell of.
 */
object VirtualDesktops {
    private val comThread = Executors.newSingleThreadExecutor { Thread(it, "Claude Sessions: virtual desktops").apply { isDaemon = true } }

    /** The `IVirtualDesktopManager`, made on [comThread] as it's first needed. */
    private val manager: VirtualDesktopManager by lazy {
        COMUtils.checkRC(Ole32.INSTANCE.CoInitializeEx(null, Ole32.COINIT_MULTITHREADED))
        val made = PointerByReference()
        COMUtils.checkRC(Ole32.INSTANCE.CoCreateInstance(CLSID, null, WTypes.CLSCTX_ALL, IID, made))
        VirtualDesktopManager(made.value)
    }

    /** The desktop [window] is on, or null when it's on none, as while it isn't shown, or elsewhere than on Windows. */
    fun desktopOf(window: WindowHandle): VirtualDesktopId? =
        if (!SystemInfo.isWindows) null else onTheComThread { manager.desktopOf(window.hwnd) }

    /**
     * Moves [window], one of this process's, to [desktop], and returns whether it did: not when the desktop no longer
     * exists, as after a reboot, or elsewhere than on Windows.
     */
    fun move(window: WindowHandle, desktop: VirtualDesktopId): Boolean =
        SystemInfo.isWindows && onTheComThread { manager.move(window.hwnd, desktop) }

    private fun <T> onTheComThread(call: () -> T): T = comThread.submit(Callable(call)).get()

    /** The `IVirtualDesktopManager` COM object [pointer] points to: its methods, by their places in its table of methods. */
    private class VirtualDesktopManager(pointer: Pointer) : Unknown(pointer) {
        fun desktopOf(window: WinDef.HWND): VirtualDesktopId? {
            val desktop = Guid.GUID.ByReference()
            val result = _invokeNativeInt(GET_WINDOW_DESKTOP_ID, arrayOf(pointer, window, desktop))
            return if (COMUtils.SUCCEEDED(result) && desktop.toUuid() != NO_DESKTOP) VirtualDesktopId(desktop.toUuid()) else null
        }

        fun move(window: WinDef.HWND, desktop: VirtualDesktopId): Boolean =
            COMUtils.SUCCEEDED(_invokeNativeInt(MOVE_WINDOW_TO_DESKTOP, arrayOf(pointer, window, Guid.GUID.ByReference(Guid.GUID("{${desktop.value}}")))))

        private fun Guid.GUID.toUuid(): UUID = UUID.fromString(toGuidString().removeSurrounding("{", "}"))
    }

    /** The class of the `VirtualDesktopManager` COM object, as Windows' `shobjidl_core.h` names it. */
    private val CLSID = Guid.CLSID("{aa509086-5ca9-4c25-8f95-589d3c07b48a}")

    /** The interface `IVirtualDesktopManager`. */
    private val IID = Guid.IID("{a5cd92ff-29be-454c-8d04-d82879fb3f1b}")

    /** `GetWindowDesktopId`'s place in the table of methods: after `IUnknown`'s three and `IsWindowOnCurrentVirtualDesktop`. */
    private const val GET_WINDOW_DESKTOP_ID = 4

    /** `MoveWindowToDesktop`'s place in the table of methods. */
    private const val MOVE_WINDOW_TO_DESKTOP = 5

    /** The id `GetWindowDesktopId` gives a window on no desktop, as one not shown: all zeros. */
    private val NO_DESKTOP = UUID(0, 0)
}
