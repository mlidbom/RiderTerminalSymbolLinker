import org.jetbrains.changelog.Changelog
import org.jetbrains.intellij.platform.gradle.tasks.VerifyPluginTask

// The plugins' versions are the root build's: Gradle applies a plugin the root build has applied without a version.
plugins {
    kotlin("jvm")
    id("org.jetbrains.intellij.platform")
    id("org.jetbrains.changelog")
}

group = "dev.magnus"
version = "0.2.0"

repositories {
    mavenCentral()
    intellijPlatform {
        defaultRepositories()
    }
}

dependencies {
    intellijPlatform {
        // Any IDE of the platform will do to build against, as the plugin uses only the platform and its JNA: the
        // installed Rider locally, as for the symbol linker, and in CI the pinned Rider the symbol linker's build
        // downloads anyway.
        val localIdePath = providers.gradleProperty("localIdePath").orNull
        if (!localIdePath.isNullOrBlank()) {
            local(localIdePath)
        } else {
            rider(providers.gradleProperty("platformVersion").get())
        }
        zipSigner()
        // The platform's JNA, to move the IDE's windows back to their virtual desktops through Windows' COM API.
        bundledPlugin("com.intellij.modules.jna")
    }
}

// Named without the version, as the symbol linker's jar is, so that an IDE started with idea.auto.reload.plugins
// reloads a new build without a restart: see the symbol linker's build.gradle.kts.
tasks.named<org.gradle.jvm.tasks.Jar>("composedJar") {
    archiveFileName = "ClaudeSessions.jar"
}

intellijPlatform {
    pluginConfiguration {
        ideaVersion {
            // 263 = 2026.3: the plugin claims only the IDEs it's been tried in. No upper bound, as for the symbol linker.
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
        // Every finding fails the build, as for the symbol linker.
        failureLevel = VerifyPluginTask.FailureLevel.ALL
    }
}

changelog {
    repositoryUrl = "https://github.com/mlidbom/RiderTerminalSymbolLinker"
}

kotlin {
    // 21, the platform's bytecode level: see the symbol linker's build.gradle.kts.
    jvmToolchain(21)
    // No compatibility bridges for the platform interfaces' default methods: a class implementing AppLifecycleListener
    // would otherwise override each of them, deprecated ones included, which the Plugin Verifier reports as uses of
    // deprecated and experimental API.
    compilerOptions {
        jvmDefault = org.jetbrains.kotlin.gradle.dsl.JvmDefaultMode.NO_COMPATIBILITY
    }
}
