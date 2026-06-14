import org.jetbrains.changelog.Changelog

plugins {
    kotlin("jvm") version "2.4.0"
    id("org.jetbrains.intellij.platform") version "2.16.0"
    id("org.jetbrains.changelog") version "2.5.0"
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
        // The ReSharper MCP we declare a hard <depends> on in plugin.xml. Pulled from the Marketplace
        // so the sandbox/verifier can resolve the dependency; it is NOT bundled into our distribution.
        plugin("com.j-light.resharper-mcp", "0.9.1")
    }

    // Plain JUnit 5 for the pure unit tests (FileReferences / SolutionFileIndex). They touch no platform
    // types, so they need no IntelliJ test fixture — just the JUnit engine.
    testImplementation(platform("org.junit:junit-bom:5.10.2"))
    testImplementation("org.junit.jupiter:junit-jupiter")
    testRuntimeOnly("org.junit.platform:junit-platform-launcher")
}

tasks.test {
    useJUnitPlatform()
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
        // Per-version "What's new" on the Marketplace listing, rendered from this version's CHANGELOG.md
        // section (falls back to Unreleased if the version has no section yet).
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

    // signPlugin/publishPlugin read these from the environment; buildPlugin alone needs none. CI
    // supplies the signing vars on release; publishPlugin additionally needs PUBLISH_TOKEN. See README.
    signing {
        certificateChain = providers.environmentVariable("CERTIFICATE_CHAIN")
        privateKey = providers.environmentVariable("PRIVATE_KEY")
        password = providers.environmentVariable("PRIVATE_KEY_PASSWORD")
    }
    publishing {
        token = providers.environmentVariable("PUBLISH_TOKEN")
    }
}

// Single source of truth for change notes (Keep a Changelog format): feeds both the Marketplace
// changeNotes above and the GitHub release body (CI runs `getChangelog` — see release.yml).
changelog {
    repositoryUrl = "https://github.com/mlidbom/RiderTerminalSymbolLinker"
}

kotlin {
    jvmToolchain(21)
}
