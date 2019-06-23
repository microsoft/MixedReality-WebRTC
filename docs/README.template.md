# Generated documentation branch `docs/$branchname`

This branch `docs/$branchname` is an orphan branch for the generated documentation of the associated repository branch `$branchname`. **This branch cannot be committed to manually**.

To edit the documentation of `$branchname`:

- **User manual**: edit files in [the `docs/` directory of the branch `$branchname`](https://github.com/microsoft/MixedReality-WebRTC/tree/$branchname/docs)
- **API reference**: modify the comments [in the library source code of the branch `$branchname`](https://github.com/microsoft/MixedReality-WebRTC/tree/$branchname/libs)

Once merged the `ci-docs` build pipeline will pick up the changes in `$branchname`, generate a new documentation from them, and automatically commit the result to the associated `docs/$branchname` branch used as source for the documentation website.