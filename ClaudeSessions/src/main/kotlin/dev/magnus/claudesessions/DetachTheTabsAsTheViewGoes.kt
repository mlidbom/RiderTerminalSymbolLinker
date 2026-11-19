package dev.magnus.claudesessions

import com.intellij.ide.AppLifecycleListener
import com.intellij.ide.plugins.DynamicPluginListener
import com.intellij.ide.plugins.IdeaPluginDescriptor
import com.intellij.openapi.project.Project
import com.intellij.openapi.project.ProjectCloseListener
import com.intellij.openapi.project.ProjectManager

/**
 * Has each project's [ClaudeSessionsView] detach its tabs from their session hosts as the view goes though the user
 * didn't close them: as the project closes, the IDE exits, or this plugin unloads, as for an update. The tabs' sessions
 * run on in their hosts, and the view attaches to them again when it next opens: see [ClaudeSessionsView.detachTheTabs].
 *
 * Until 2026-11-19, when a tab's terminal ran its shell itself, the plugin's unload parked the tabs in the Terminal tool
 * window, so that their sessions ran on, and the reloaded plugin took them back; the IDE exiting ended them.
 */
class DetachTheTabsAsTheViewGoes : ProjectCloseListener, AppLifecycleListener, DynamicPluginListener {
    override fun projectClosing(project: Project) {
        ClaudeSessionsView.getInstance(project).detachTheTabs()
    }

    override fun appWillBeClosed(isRestart: Boolean) {
        ProjectManager.getInstance().openProjects.forEach(::projectClosing)
    }

    override fun beforePluginUnload(pluginDescriptor: IdeaPluginDescriptor, isUpdate: Boolean) {
        //bug: the unloaded build stays in memory: the IDE logs "Plugins that were expected to unload but are still
        //   referenced", and tells the user the plugin "didn't unload fully". Seen at every reload with tabs parked in the
        //   Terminal tool window until 2026-11-19, and in the sandbox on 2026-11-19 with session hosts, nothing parked, after
        //   a reload with tabs open; with no tab open, the unload was clean (2026-11-17). A lead (jcmd GC.class_histogram on
        //   Magnus's Rider, 2026-11-18): of each old build, an instance of ClaudeSessionsToolWindowFactory and one of
        //   NewClaudeSessionAction were still alive, and none of its views, tabs, keepers, coroutines or local API servers.
        //   Next: the live heap, with a debugger. The platform's snapshot of a failed unload (registry
        //   ide.plugins.snapshot.on.unload.fail) wrote nothing in Rider.
        if (pluginDescriptor.pluginId.idString == PLUGIN_ID) ProjectManager.getInstance().openProjects.forEach(::projectClosing)
    }

    private companion object {
        /** This plugin's id, as plugin.xml declares it. */
        const val PLUGIN_ID = "dev.magnus.claudesessions"
    }
}
