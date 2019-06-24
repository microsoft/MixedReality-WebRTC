# Generated documentation branch

This branch `gh-pages` is an orphan branch for the generated documentation of MixedReality-WebRTC. **This branch cannot be committed to manually**.

To edit the documentation of a branch:

- **User manual**: edit files in [the `docs/` directory of the branch](https://github.com/microsoft/MixedReality-WebRTC/tree/master/docs)
- **API reference**: modify the comments [in the library source code of the branch](https://github.com/microsoft/MixedReality-WebRTC/tree/master/libs)

Once merged the `ci-docs` build pipeline will pick up the changes in the branch, generate a new documentation from them, and automatically commit the result to this branch used as source for the documentation website.
