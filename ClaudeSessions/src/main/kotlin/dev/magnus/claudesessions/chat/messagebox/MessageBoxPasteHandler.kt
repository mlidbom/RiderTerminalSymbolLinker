package dev.magnus.claudesessions.chat.messagebox

import com.intellij.openapi.actionSystem.DataContext
import com.intellij.openapi.editor.Caret
import com.intellij.openapi.editor.Editor
import com.intellij.openapi.editor.actionSystem.EditorActionHandler
import com.intellij.openapi.ide.CopyPasteManager

/**
 * The IDE's paste, Ctrl+V or whatever the keymap binds it to, in a chat's [MessageBox]: pastes what is on the
 * clipboard through [MessageBox.paste], so that the box knows what was pasted, and takes an image. In every other
 * editor, the paste the IDE had before: [originalHandler].
 *
 * Registered in plugin.xml as the first handler of the editor action `EditorPaste`, which the IDE's paste runs in an
 * editor.
 */
class MessageBoxPasteHandler(private val originalHandler: EditorActionHandler) : EditorActionHandler() {
    override fun doExecute(editor: Editor, caret: Caret?, dataContext: DataContext?) {
        val box = editor.getUserData(MessageBox.KEY) ?: return originalHandler.execute(editor, caret, dataContext)
        box.paste(CopyPasteManager.getInstance().contents)
    }

    override fun isEnabledForCaret(editor: Editor, caret: Caret, dataContext: DataContext?): Boolean =
        editor.getUserData(MessageBox.KEY) != null || originalHandler.isEnabled(editor, caret, dataContext)
}
