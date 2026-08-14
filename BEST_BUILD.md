# Current best build

The current user-approved baseline is:

`backups/Deploy-20260810-195835-BEST-1352-AllIslandExclusions`

Single release archive:

`backups/Deploy-20260810-195835-BEST-1352-AllIslandExclusions.zip`

ZIP SHA-256: `14271DD25BC7F70B50E3E6BDCD2AE4381B1E945364EA467C6DB6330ECC9BF821`

It packages the untouched August 10 13:52 main DLL together with the approved
high-resolution southwestern and northern-chain island-exclusion sidecar and its
required `SubModule.xml` entry.

Do not replace the main DLL from the current source tree when restoring this
baseline. Restore all files from the package together and verify them against
`BUILD_MANIFEST.md`.
