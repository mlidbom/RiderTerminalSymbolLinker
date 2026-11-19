package dev.magnus.claudesessions.windows

import java.util.UUID

/** A virtual desktop of Windows, by the GUID Windows identifies it with, which stays the same across restarts. */
@JvmInline
value class VirtualDesktopId(val value: UUID)
