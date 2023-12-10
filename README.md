# WhisperAPI

[![GitHub license](https://img.shields.io/github/license/DontEatOreo/WhisperAPI)](https://github.com/DontEatOreo/WhisperAPI/blob/master/LICENSE.txt)
[![GitHub release](https://img.shields.io/github/release/DontEatOreo/WhisperAPI)](https://github.com/DontEatOreo/WhisperAPI/releases)
[![GitHub issues](https://img.shields.io/github/issues/DontEatOreo/WhisperAPI)](https://github.com/DontEatOreo/WhisperAPI/issues)

WhisperAPI is a wrapper for [Whisper.cpp](https://github.com/ggerganov/whisper.cpp) a C++ implementation of the original OpenAI Whisper that greatly enhances its performance and speed.

## AppSettings

You will need to edit the `appsettings.json` file to contain a full path to where you want to store models and audio files.

```json
{
  "WhisperSettings": {
    "Folder": "/path/to/whisper/folder"
  }
}
```

In the `Folder` property you will need to provide a full path to where you want to store models and audio files.

## Note

Translation increase the processing time, sometimes 2x the time! So avoid translation for long videos or audios.

## Features

- Transcribe video and audio files into text
- Supports all models
- Easy to use and integrate into your own projects
- Fast and reliable transcription results
- Supports every language by OpenAI Whisper
- Ability to translate transcribed text to English

### Notes

- You can use any language codes supported by OpenAI Whisper
- If you're unsure or don't know ahead of time which language code you need you can omit lang property.
- Supported Models are: Tiny, Base, Medium and Large.

## Usage

Before making a request to transcribe a file, you should query the `/models` endpoint to get a list of all available models.

```shell
curl --location --request GET 'https://localhost:5001/models'
```

To use WhisperAPI, you need to send a POST request to the `/transcribe` endpoint with the following form-data payload:

```
file: @/path/to/file/
model: String
translate: Boolean
```

Additionally, you can add headers to the request for language and response type preferences.

```
Accept: application/json
Accept-Language: en
```

The file should be provided as a multipart/form-data field named ``file``.

`translate` is an optional property.

- If the `Accept` header is omitted, the API will automatically detect the language of the file.
- If the `translate` property is omitted, it defaults to false.

Here is an example of a request using curl:

```shell
curl --location --request POST 'https://localhost:5001/transcribe' \
--header 'Accept: application/json' \
--header 'Accept-Language: English' \
--form 'file=@"/path/to/file/"' \
--form 'model="base"' \
--form 'translate="true"'
```

The response will be a JSON payload with the following format:

```json
{
  "data": [
    {
      "start": 0,
      "end": 3,
      "text": "Hello!"
    },
    {
      "start": 3,
      "end": 6,
      "text": " World!"
    }
  ],
  "count": 2
}
```

If `text/plain` is used the response will look like this:

```text
Hello! World!
```

If `application/xml` is used the response will look like this:

```xml
<JsonResponse>
    <Data>
        <ResponseData>
            <Start>0</Start>
            <End>3</End>
            <Text>Hello</Text>
        </ResponseData>
        <ResponseData>
            <Start>3</Start>
            <End>6</End>
            <Text> World!</Text>
        </ResponseData>
    </Data>
    <Count>2</Count>
</JsonResponse>
```

On failure (e.g: invalid file format) the response JSON will be:

```json
{
  "error": "Error message"
}
```

## Contributing

We welcome contributions to WhisperAPI! If you would like to contribute, simply fork the repository and submit a pull request with your changes.

## Support

If you need help with WhisperAPI, please create an issue on GitHub and I will respond as soon as possible.
