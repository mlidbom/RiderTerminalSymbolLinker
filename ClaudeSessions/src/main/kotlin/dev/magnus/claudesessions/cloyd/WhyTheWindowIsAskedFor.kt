package dev.magnus.claudesessions.cloyd

/**
 * Why Claude Sessions asks Cloyd for a project's window, which it tells Cloyd with `Cloyd.exe --project <folder>
 * --because <why>`: Cloyd's log tells it, and while Cloyd's Projects Dashboard is open, Cloyd opens no window
 * [IDE_OPENED_THE_PROJECT] asks for, since the dashboard shows the project. A Cloyd from before 2026-11-24 ignores it.
 *
 * @property inTheRequest How Cloyd's local API names it, in `POST /windows/show`'s `because`.
 */
enum class WhyTheWindowIsAskedFor(val inTheRequest: String) {
    /** The IDE opened the project, with no word from the user: [ShowTheProjectInCloyd]. */
    IDE_OPENED_THE_PROJECT("ideOpenedTheProject"),

    /** The user asked, with Tools > Show the Project in Cloyd: [ShowTheProjectInCloydAction]. */
    THE_USER_ASKED("magnusAsked"),
}
