import org.jetbrains.changelog.Changelog
import org.jetbrains.intellij.platform.gradle.tasks.VerifyPluginTask

// The plugins' versions are the root build's: Gradle applies a plugin the root build has applied without a version.
plugins {
    kotlin("jvm")
    kotlin("plugin.compose")
    id("org.jetbrains.intellij.platform")
    id("org.jetbrains.changelog")
}

group = "dev.magnus"
version = "0.1.0"

repositories {
    mavenCentral()
    intellijPlatform {
        defaultRepositories()
    }
}

dependencies {
    intellijPlatform {
        // Any IDE of the platform will do to build against, as the plugin uses only the platform and the terminal: the
        // installed Rider locally, as for the symbol linker, and in CI the pinned Rider the symbol linker's build
        // downloads anyway.
        val localIdePath = providers.gradleProperty("localIdePath").orNull
        if (!localIdePath.isNullOrBlank()) {
            local(localIdePath)
        } else {
            rider(providers.gradleProperty("platformVersion").get())
        }
        zipSigner()
        // The reworked terminal's API: its tabs, and sending a command to one.
        bundledPlugin("org.jetbrains.plugins.terminal")
        // The platform's JNA, to call Windows' AllowSetForegroundWindow.
        bundledPlugin("com.intellij.modules.jna")
        // The type of the terminal's environment setting, EnvironmentVariablesData, which a session host's shell gets:
        // on the IDE's classpath, and not on the one the build compiles against without it.
        bundledModule("intellij.platform.execution.impl")
        // The platform's Compose and Jewel, in which the chat's transcript renders the conversation's markdown, styled
        // as the IDE is: the modules of the bundled plugin com.intellij.modules.compose, which plugin.xml depends on.
        bundledModule("intellij.libraries.compose.runtime.desktop")
        bundledModule("intellij.libraries.compose.foundation.desktop")
        bundledModule("intellij.libraries.skiko")
        bundledModule("intellij.platform.jewel.foundation")
        bundledModule("intellij.platform.jewel.ui")
        bundledModule("intellij.platform.jewel.ideLafBridge")
        bundledModule("intellij.platform.jewel.markdown.core")
        bundledModule("intellij.platform.jewel.markdown.ideLafBridgeStyling")
        bundledModule("intellij.platform.jewel.markdown.extensions.gfmTables")
        bundledModule("intellij.platform.jewel.markdown.extensions.gfmStrikethrough")
        bundledModule("intellij.platform.jewel.markdown.extensions.autolink")
    }

    // Plain JUnit Jupiter, as for the symbol linker's unit tests: the contract test of the hub's local API touches no
    // platform type that needs a test fixture.
    testImplementation(platform("org.junit:junit-bom:6.1.3"))
    testImplementation("org.junit.jupiter:junit-jupiter")
    testRuntimeOnly("org.junit.platform:junit-platform-launcher")
    // IntelliJ's JUnit5TestSessionListener, on the test classpath with the IDE's bundled plugins, needs JUnit 4: see the
    // symbol linker's build.gradle.kts.
    testRuntimeOnly("junit:junit:4.13.2")
}

tasks.test {
    useJUnitPlatform()
}

// Named without the version, as the symbol linker's jar is, so that an IDE started with idea.auto.reload.plugins
// reloads a new build without a restart: see the symbol linker's build.gradle.kts.
tasks.named<org.gradle.jvm.tasks.Jar>("composedJar") {
    archiveFileName = "ClaudeSessions.jar"
}

intellijPlatform {
    pluginConfiguration {
        ideaVersion {
            // 263 = 2026.3: the reworked terminal's tab API that the plugin uses was published with 2025.3, and is
            // experimental, so the plugin claims only the IDEs it's been tried in. No upper bound, as for the symbol
            // linker.
            sinceBuild = "263"
            untilBuild = provider { null }
        }
        changeNotes = provider {
            with(changelog) {
                renderItem(
                    (getOrNull(project.version.toString()) ?: getUnreleased())
                        .withHeader(false)
                        .withEmptySections(false),
                    Changelog.OutputType.HTML,
                )
            }
        }
    }

    pluginVerification {
        ides {
            val localIdePath = providers.gradleProperty("localIdePath").orNull
            if (!localIdePath.isNullOrBlank()) {
                local(file(localIdePath))
            } else {
                create(org.jetbrains.intellij.platform.gradle.IntelliJPlatformType.Rider, providers.gradleProperty("platformVersion").get())
            }
        }
        // Every finding fails the build, as for the symbol linker, but the use of experimental API: the reworked
        // terminal's API, which the plugin is built on, is experimental as a whole since 2025.3, as the SDK's "Embedded
        // Terminal" page says. The verifier lists each use; check them after each new IDE.
        failureLevel = VerifyPluginTask.FailureLevel.ALL.filter { it != VerifyPluginTask.FailureLevel.EXPERIMENTAL_API_USAGES }
    }
}

changelog {
    repositoryUrl = "https://github.com/mlidbom/RiderTerminalSymbolLinker"
}

kotlin {
    // 21, the platform's bytecode level: see the symbol linker's build.gradle.kts.
    jvmToolchain(21)
    // No compatibility bridges for the platform interfaces' default methods: a class implementing ToolWindowFactory or
    // DynamicPluginListener would otherwise override each of them, deprecated ones included, which the Plugin Verifier
    // reports as uses of deprecated and experimental API.
    compilerOptions {
        jvmDefault = org.jetbrains.kotlin.gradle.dsl.JvmDefaultMode.NO_COMPATIBILITY
        // Jewel's markdown rendering, which the chat's transcript is built on, is experimental as a whole, as Jewel is
        // below 1.0: its uses are checked as the platform's Jewel changes, with each new IDE.
        optIn.add("org.jetbrains.jewel.foundation.ExperimentalJewelApi")
    }
}
