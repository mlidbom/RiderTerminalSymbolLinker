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
        // The .NET Analysis&Refactoring MCP, which plugin.xml has a hard <depends> on, is not a build
        // dependency: no code here references it (we talk to it over HTTP), and it isn't on the Marketplace
        // to be pulled from yet. Once it is, adding plugin("dev.magnus.dotnet-analysis-and-refactoring-mcp",
        // "<version>") here lets a runIde sandbox load this plugin.
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
            // 261 = Rider 2026.1 (the stable CI build target). No upper bound: a cap disables the plugin for
            // every user on each new Rider until a release raises it, as Rider 2026.3 did to 0.2.7 though
            // nothing in the code needed changing. The re-highlight and terminal-context-menu hooks rely
            // on reworked-terminal internals that can shift, so check them after each new Rider.
            // `provider { null }` omits until-build; leaving the property out would default it to the
            // build platform's MAJOR.* (261.* in CI).
            sinceBuild = "261"
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
}

// Single source of truth for change notes (Keep a Changelog format): feeds both the Marketplace
// changeNotes above and the GitHub release body (CI runs `getChangelog` — see release.yml).
changelog {
    repositoryUrl = "https://github.com/mlidbom/RiderTerminalSymbolLinker"
}

kotlin {
    jvmToolchain(21)
}
