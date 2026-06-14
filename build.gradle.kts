plugins {
    kotlin("jvm") version "2.4.0"
    id("org.jetbrains.intellij.platform") version "2.16.0"
}

group = "dev.magnus"
version = "0.5.3-spike"

repositories {
    mavenCentral()
    intellijPlatform {
        defaultRepositories()
    }
}

dependencies {
    intellijPlatform {
        // Build against the locally installed Rider (no multi-GB platform download).
        local("C:/Users/magnu/AppData/Local/Programs/Rider")
    }
}

intellijPlatform {
    pluginConfiguration {
        ideaVersion {
            // Sideload target: Rider 2026.2 (build 262.7581.24).
            sinceBuild = "262"
            untilBuild = "262.*"
        }
    }
}

kotlin {
    jvmToolchain(21)
}
