package dev.magnus.claudesessions.reload

import com.intellij.ide.plugins.DynamicPluginListener
import com.intellij.ide.plugins.IdeaPluginDescriptor
import com.intellij.openapi.project.ProjectManager
import dev.magnus.claudesessions.ClaudeSessionsView

/**
 * Parks the tabs of every open project's [ClaudeSessionsView] in the Terminal tool window as this plugin unloads, so
 * that their Claude Code sessions run on while it reloads: see [TabsParkedInTheTerminal].
 */
class ParkTheTabsWhileThePluginReloads : DynamicPluginListener {
    override fun beforePluginUnload(pluginDescriptor: IdeaPluginDescriptor, isUpdate: Boolean) {
        if (pluginDescriptor.pluginId.idString != PLUGIN_ID) return
        ProjectManager.getInstance().openProjects.forEach { ClaudeSessionsView.getInstance(it).parkTheTabs() }
    }

    private companion object {
        /** This plugin's id, as plugin.xml declares it. */
        const val PLUGIN_ID = "dev.magnus.claudesessions"
    }
}
