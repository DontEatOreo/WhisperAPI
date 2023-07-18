# WhisperAPI

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

### Notes:

- You can use any language codes supported by OpenAI Whisper (You can also use language names like English, German, Japanese, etc or language codes like en, de, ja, etc)
- If you're unsure or don't know ahead of time which country code you need you can omit lang property.
- Supported Models are: Tiny, Base, Medium and Large.

## Usage

To use WhisperAPI, you will need to send a POST multipart/form-data to the ``/transcribe`` endpoint with the following JSON payload:
```json
{
    "lang": "en",
    "model": "base",
    "translate": true 
}
```
And with the file as a multipart/form-data field named ``file``.

`lang` and `translate` are optional properties.
- If `lang` is omitted, it will automatically detect the language of the file.
- If `translate` is omitted, it will default to false.

Here is a curl example of the request:
```bash
curl -X POST \
  -H "Content-Type: multipart/form-data" \
  -F "file=@/path/to/file.mp3" \
  -F "lang=en" \
  -F "model=base" \
  https://localhost:5001/transcribe
```

The response will be a JSON payload with the following format:
```json
{
  "data": [
    {
      "start": 0,
      "end": 2.30,
      "text": "Hello",
    },
    {
      "start": 2.30,
      "end": 3.40,
      "text": "World",
    }
  ],
  "count": 11
}
```

On failure (e.g: invalid file format) the response JSON payload will be:
```json
{
  "error": "Error Message"
}
```

## Contributing

We welcome contributions to WhisperAPI! If you would like to contribute, simply fork the repository and submit a pull request with your changes.

## Support

If you need help with WhisperAPI, please create an issue on GitHub and I will respond as soon as possible.