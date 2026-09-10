# Dots Arena - Android 아이콘 세트

현재 Unity 프로젝트의 Android 아이콘 슬롯 크기에 맞춘 파일입니다.

## Unity 적용 위치

`Project Settings > Player > Android > Icon`

### Legacy

`Legacy` 폴더에서 같은 크기의 파일을 지정합니다.

- 36 x 36
- 48 x 48
- 72 x 72
- 96 x 96
- 144 x 144
- 192 x 192

### Round

`Round` 폴더에서 같은 크기의 파일을 지정합니다. 원 밖은 투명 처리되어 있습니다.

- 36 x 36
- 48 x 48
- 72 x 72
- 96 x 96
- 144 x 144
- 192 x 192

### Adaptive

각 슬롯의 Foreground에는 `Adaptive/Foreground`, Background에는 `Adaptive/Background` 폴더에서 같은 크기의 파일을 지정합니다.

- 81 x 81
- 108 x 108
- 162 x 162
- 216 x 216
- 324 x 324
- 432 x 432

`Adaptive/Preview`는 두 레이어를 합친 확인용 이미지이므로 Unity 슬롯에는 지정하지 않습니다.

## Google Play

스토어 등록용 512 x 512 아이콘은 `../../GooglePlay/DotsArena_AppIcon_512.png`를 사용합니다.

## 적응형 전경 제작 방식

- 도구: OpenAI 기본 이미지 생성 도구의 배경 추출 편집
- 대상: 기존 Dots Arena 앱 아이콘
- 프롬프트: 어두운 남색 배경만 제거하고 네 개의 구슬, 연결 막대, 황금색 칸, 왕관과 노란 강조선을 보존한 투명 배경의 Android 적응형 아이콘 전경을 제작. 글자, 워터마크, 추가 오브젝트, 마스크 형태와 외곽 배경은 추가하지 않음.

나머지 규격 파일은 원본을 고품질 보간으로 축소했습니다.
