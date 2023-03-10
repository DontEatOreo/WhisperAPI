# WhisperAPI

WhisperAPI is a wrapper for [Whisper.cpp](https://github.com/ggerganov/whisper.cpp) a C++ implementation of the original OpenAI Whisper that greatly enhances its performance and speed.

## Features

- Transcribe video and audio files into text
- Supports all models
- Easy to use and integrate into your own projects
- Fast and reliable transcription results
- Supports every language by OpenAI Whisper
- Ability to translate transcribed text to English

## Contributing

We welcome contributions to WhisperAPI! If you would like to contribute, simply fork the repository and submit a pull request with your changes.

## Support

If you need help with WhisperAPI, please create an issue on GitHub and I will respond as soon as possible.

## Limitations
- The number of files that can be processed simultaneously depends on the number of CPU threads available. For instance, with four threads, two files can be processed simultaneously. However, with only one thread, only one file can be processed at a time.
- File size limit is 50 MiB.

## Environment Variables
`WHISPER_FOLDER` - The folder where the whisper files will be stored. Default is `~/.config/whisper`
`WHISPER_EXEC_PATH` - The path to the whisper executable. Default is `WHISPER_FOLDER/main`

### Notes:

- You can use any country code supported by OpenAI Whisper (You can also use language names like English, German, Japanese, etc)
- If you're unsure or don't know ahead of time which country code you need you can omit lang property.
- Supported Models are: Tiny, Base, Medium and Large. If Model property is omit it will default to base
- If a model is missing it's going to be automatically downloaded to `WHISPER_FOLDER`
- If you don't have `FFmpeg` and/or `WhisperCPP` installed the API won't start. You can download them from the following links:

## Usage

To use WhisperAPI, you will need to send a POST request to the ``/transcribe`` endpoint with the following JSON payload:
```json
{
    "file": "...base64 encoded file...",
    "lang": "en",
    "time_stamps": false,
    "model": "base"
}
```
`lang`, `time_stamps` and `model` are optional properties.
- If `lang` is omitted, it will automatically detect the language of the file.
- If `time_stamps` is omitted, it will default to false.
- If `model` is omitted, it will default to base.

Here is a curl example of the request:
```bash
curl --location --request POST 'https://localhost:7272/transcribe' \
--header 'Content-Type: application/json' \
--data-raw '{
    "file": ???...base64 encoded file...???,
    "lang": "en",
    "time_stamps": false,
    "model": "base"
}'
```

The response will be a JSON payload with the following format:
```json
{
    "success": true,
    "result": "Hello World"
}
```

If the request had ``time_stamps`` set to true, you will instead receive a result with an array like this:
- `start` and `end` is the period of time in seconds which something was said.
```json
{
  "success": true,
  "result": [
    {
      "start": 0,
      "end": 5,
      "text": "Hello"
    },
    {
      "start": 5,
      "end": 10,
      "text": "World"
    }
  ]
}
```

On failure (e.g: invalid file format) the response JSON payload will be:
```json
{
    "success": false,
    "error_code": "...",
    "error_message": "..."
}
```