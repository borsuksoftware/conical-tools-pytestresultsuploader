Source code for BorsukSoftware.Conical.Tools.PytestResultsUploader

For more details on Conical, please see https://conical.cloud

## Purpose
The tool exists to make it easy to upload results from pytest runsto Conical.

The tool assumes that the user has run pytest with the -rA flag.

#### Artefacts / Screenshot support
The tool supports the concept of an artefacts directory. Within this directory, any file prefixed with the name of a valid test (case sensitive) will be uploaded as an additional file.

## Usage
The expectation is that this tool is used as part of CI pipelines where pytest is used as well as when users are using AWS Device Farm to run appium python tests and they wish to be able to run the same set of tests locally and upload the results for distribution to COnical.

## Examples
Once the tool has been installed, it can be run with:

```
dotnet tool run BorsukSoftware.Conical.Tools.PyTestResultsUploader
  -server "https://demo.conical.cloud" `
  -token "itsNotOurTokenDontEvenBother:)" `
  -product "AWS-Testing" `
  -testRunType "Appium" `
  -testRunSetName "Local Run" `
  -logFile "pytestLogs.txt" `
  -artefactsDirectory "artefacts"

```

## FAQs
#### There's a bug, what do we do?
Contact us / raise an issue / PR.

#### Our use-case is slightly different, what do we do?
Contact us to see how we can help?

#### We just want to be able to parse pytest logs, how can we use this library to do so?
We supply BorsukSoftware.Utils.Pytest as a library on nuget which does the actual parsing, simply use that.