# Seq.App.HipChat

An app for Seq (http://getseq.net) that forwards messages to HipChat.

## Changes

### 1.1.0

- Option for setting custom HipChat install base URL
- Links to Seq from HipChat messages are now compatible with Seq v2

## Building NuGet Package

From solution root, run:

- msbuild
- nuget pack ./Seq.App.HipChat/Seq.App.HipChat.nuspec