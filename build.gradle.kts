import org.jetbrains.changelog.Changelog
import org.jetbrains.intellij.platform.gradle.IntelliJPlatformType
import org.jetbrains.intellij.platform.gradle.tasks.VerifyPluginTask

plugins {
    kotlin("jvm") version "2.4.21"
    id("org.jetbrains.intellij.platform") version "2.20.1"
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
        // The Marketplace ZIP Signer that signPlugin runs. The Gradle plugin's docs require declaring it. This build
        // resolved one without it, but the .NET Analysis&Refactoring MCP's identical setup found none on CI and its
        // release failed at signing (2026-11-17), so it is declared, as there.
        zipSigner()
        // Provides the Terminal.ReworkedTerminalContextMenu group our refresh action attaches to.
        bundledPlugin("org.jetbrains.plugins.terminal")
        // The .NET Analysis&Refactoring MCP we declare a hard <depends> on in plugin.xml. Pulled from the
        // Marketplace so the sandbox/verifier can resolve the dependency; it is NOT bundled into our distribution.
        plugin("dev.magnus.dotnet-analysis-and-refactoring-mcp", "0.14.0")
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
            // 263 = Rider 2026.3 (the stable CI build target), the earliest Rider the .NET Analysis&Refactoring
            // MCP supports from its 0.14.0 on: claiming an older Rider than the dependency would offer users a
            // plugin they can't satisfy. No upper bound: a cap disables the plugin for every user on each new
            // Rider until a release raises it, as Rider 2026.3 did to 0.2.7 though nothing in the code needed
            // changing. The re-highlight and terminal-context-menu hooks rely on reworked-terminal internals
            // that can shift, so check them after each new Rider.
            // `provider { null }` omits until-build; leaving the property out would default it to the
            // build platform's MAJOR.* (263.* in CI).
            sinceBuild = "263"
            untilBuild = provider { null }
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

    // The Marketplace runs the Plugin Verifier on every upload, and this plugin's updates have been approved at once
    // since its reports had no findings (Magnus, 2026-11-14). So verifyPlugin fails on any finding: locally against
    // the installed Rider, in CI against the pinned Rider (the oldest the plugin is built for) and the latest release.
    // Not the recommended() set, which may be a Rider for every version since sinceBuild: more than a runner's disk
    // holds. The .NET Analysis&Refactoring MCP's build has the same gate; keep the two release setups alike.
    pluginVerification {
        ides {
            val localIdePath = providers.gradleProperty("localIdePath").orNull
            if (!localIdePath.isNullOrBlank()) {
                local(file(localIdePath))
            } else {
                create(IntelliJPlatformType.Rider, providers.gradleProperty("platformVersion").get())
                latest()
            }
        }
        failureLevel = VerifyPluginTask.FailureLevel.ALL
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
