plugins {
    kotlin("jvm") version "2.4.0"
    id("org.jetbrains.intellij.platform") version "2.16.0"
}

group = "dev.magnus"
version = providers.gradleProperty("pluginVersion").get()

repositories {
    mavenCentral()
    intellijPlatform {
        defaultRepositories()
    }
}

dependencies {
    intellijPlatform {
        // Local builds target an installed Rider (fast, no multi-GB download); CI has no local IDE,
        // so it falls back to downloading the pinned stable Rider (see gradle.properties).
        val localIdePath = providers.gradleProperty("localIdePath").orNull
        if (!localIdePath.isNullOrBlank()) {
            local(localIdePath)
        } else {
            rider(providers.gradleProperty("platformVersion").get())
        }
        // Provides the Terminal.ReworkedTerminalContextMenu group our refresh action attaches to.
        bundledPlugin("org.jetbrains.plugins.terminal")
    }
}

intellijPlatform {
    pluginConfiguration {
        ideaVersion {
            // 261 = Rider 2026.1 (the stable CI build target); capped at 262.* because the re-highlight
            // and terminal-context-menu hooks rely on reworked-terminal internals that can shift between
            // releases — bump deliberately after testing each new Rider.
            sinceBuild = "261"
            untilBuild = "262.*"
        }
    }

    // Inert unless the corresponding env vars are set — only signPlugin/publishPlugin read them, so
    // buildPlugin and the GitHub-release flow work without any secrets. See README to enable Marketplace.
    signing {
        certificateChain = providers.environmentVariable("CERTIFICATE_CHAIN")
        privateKey = providers.environmentVariable("PRIVATE_KEY")
        password = providers.environmentVariable("PRIVATE_KEY_PASSWORD")
    }
    publishing {
        token = providers.environmentVariable("PUBLISH_TOKEN")
    }
}

kotlin {
    jvmToolchain(21)
}
