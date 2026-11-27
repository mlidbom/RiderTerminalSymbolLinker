package dev.magnus.csl

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assumptions.assumeTrue
import org.junit.jupiter.api.Test
import java.io.File

/**
 * Whether the cases shared with Cloyd's specifications ([CasesSharedWithCloyd]) are a true copy of Cloyd's, the
 * original, where Cloyd's checkout is: on Magnus's machine, not in CI, where it's skipped. A change to the cases is
 * made to Cloyd's, in `~/.claude`, and copied here.
 */
class CasesSharedWithCloydTest {

    @Test
    fun `the copy has Cloyd's files, as they are`() {
        assumeTrue(CasesSharedWithCloyd.CLOYDS_CHECKOUT.resolve("src/Cloyd.Specifications").isDirectory, "No Cloyd's checkout here, to compare the copy with")
        assertEquals(
            textsIn(CasesSharedWithCloyd.CLOYDS_FOLDER),
            textsIn(CasesSharedWithCloyd.copy),
            "The copy of the cases differs from Cloyd's, the original, in ${CasesSharedWithCloyd.CLOYDS_FOLDER}: copy them over",
        )
    }

    /** The JSON files in [folder], by their names, each with its text, its lines ended alike wherever it was checked out. */
    private fun textsIn(folder: File): Map<String, String> =
        folder.listFiles { file -> file.extension == "json" }.orEmpty().associate { it.name to it.readText().replace("\r\n", "\n") }
}
