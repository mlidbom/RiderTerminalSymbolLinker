package dev.magnus.claudesessions.api;

import java.util.Arrays;
import java.util.List;
import java.util.Optional;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;

/**
 * The helper of a {@code RestartRunningACommand}: a program of its own, which the IDE starts before it exits, and which
 * waits for the IDE's process to end, runs a command, waits for it to end, and starts the IDE again. As the platform's
 * restarter runs its updater's command, which a plugin can't ask it to: the platform's API for that is internal.
 * <p>
 * Plain Java, depending on nothing: it runs on the IDE's own Java runtime, from a copy of this class outside the plugin,
 * whose files the command may replace.
 * <p>
 * Its arguments: the IDE's process id, the IDE's executable, how many seconds the IDE's exit may take, and the
 * command's program and arguments. When the IDE doesn't exit in time, as when the user cancels on the way, it runs
 * nothing and starts nothing.
 */
public final class RunsACommandBetweenTheIdesExitAndItsStart {
    public static void main(String[] arguments) throws Exception {
        long ide = Long.parseLong(arguments[0]);
        String executable = arguments[1];
        long secondsTheExitMayTake = Long.parseLong(arguments[2]);
        List<String> command = Arrays.asList(arguments).subList(3, arguments.length);

        if (!waitedForTheExitOf(ide, secondsTheExitMayTake)) {
            System.out.println("The IDE, process " + ide + ", didn't exit within " + secondsTheExitMayTake + " s: nothing ran, and it isn't started.");
            return;
        }
        System.out.println("The IDE, process " + ide + ", has exited. Running " + command);
        int exitCode = new ProcessBuilder(command).inheritIO().start().waitFor();
        System.out.println("It exited with " + exitCode + ". Starting the IDE again: " + executable);
        new ProcessBuilder(executable).start();
    }

    /** Whether the process {@code ide} has ended, within {@code seconds}. */
    private static boolean waitedForTheExitOf(long ide, long seconds) throws Exception {
        Optional<ProcessHandle> process = ProcessHandle.of(ide);
        if (process.isEmpty()) return true;
        try {
            process.get().onExit().get(seconds, TimeUnit.SECONDS);
            return true;
        } catch (TimeoutException stillRuns) {
            return false;
        }
    }
}
