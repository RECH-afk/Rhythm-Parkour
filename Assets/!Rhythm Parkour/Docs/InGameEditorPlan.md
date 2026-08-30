# План внутриигрового редактора уровней

## Идея
Игрок в игре жмёт `Tab` → открывается UI редактора поверх игры (пауза, `Time.timeScale=0`). Там он кликает **в бит** и выбирает тип препятствия. Игра сама считает `spawn = hit - travel` по невидимой стене-триггеру перед игроком, поэтому нота всегда прилетает точно в бит, даже если меняется скорость.

---

## 1. Сцена
**Не трогай `IsGameScene` для игры. Сделай новую сцену:**
- `Assets/!Rhythm Parkour/Scenes/LevelEditor.unity` — копия `IsGameScene` без `FirstPersonController`, с `FreeCamera` (скрипт выше) для облёта.
- Или аддитивно: `IsGameScene` (игра) + `LevelEditor` (UI) — загружай `SceneManager.LoadScene("LevelEditor", LoadSceneMode.Additive)` по `Tab`.

Рекомендую **новую сцену**: чище, не пачкает `IsGameScene`, можно в `Build Settings` не включать в релиз.

**Что скопировать из `IsGameScene`:**
- `Environment/Ground` (6×60), `SpawnPoint (0,0,52)`, `DespawnPoint (0,0,-8)`, `HitTrigger` (создастся авто в `RhythmParkourManager` если нет — `6×3×1 isTrigger` на `Z≈8`), `Directional Light`, `VideoQuad`.

## 2. Данные уровня — уже готово
`RhythmLevelData` (упрощён):
```csharp
music, video, cover:Sprite, fullTitle, bpm, offset, List<GameObject> obstaclePrefabs, List<ObstacleEvent> events
// ObstacleEvent: time(спаун), beat, prefabIndex, speed
```
Настройки карты (из твоего ТЗ) добавь туда же:
```csharp
[Header("Карта")]
public string songAuthor, mapAuthor;
[Header("Визуал")]
public bool particlesEnabled = true;
public Color particleColor = Color.cyan;
public Color obstacleColor = Color.white; // будет MaterialPropertyBlock
public Color trackColor = new Color(0.2f,0.6f,1f);
public bool sphereRotates = true;
```
В `RhythmParkourManager` и `ParticleSystem` применяй `particleColor/trackColor/obstacleColor` через `MaterialPropertyBlock` или `Renderer.material.color`.

Выбор файлов с диска (только в билде/эдиторе):
```csharp
#if UNITY_EDITOR
  path = EditorUtility.OpenFilePanel("Аудио","", "mp3,wav,ogg");
  // для видео — mp4
#else
  // в билде — SFB (StandaloneFileBrowser) https://github.com/gkngkc/UnityStandaloneFileBrowser
  var paths = StandaloneFileBrowser.OpenFilePanel("Аудио","", "mp3", false);
#endif
// Загрузка: UnityWebRequestMultimedia.GetAudioClip("file://"+path, AudioType.MPEG)
// Видео: VideoPlayer.url = "file://"+path
```

## 3. UI редактора (в игре) — простой
Canvas `Screen Space - Overlay`:
- **Шапка:** `Название песни / Автор песни / Автор карты` (3× InputField) + `Обложка` (Image + кнопка "Выбрать" → `OpenFilePanel` → `Texture2D.LoadImage` → `Sprite.Create`)
- **Секция трека:** `Аудио файл [Выбрать]` + `Видео файл [Выбрать]` + `BPM` + `Offset` + `Preview ▶`
- **Секция визуала:** `Toggle Партиклы` + `ColorPicker` для `particleColor/obstacleColor/trackColor` + `Toggle Сфера крутится`
- **Ноты:** Горизонтальный ScrollView — таймлайн 300px высотой (копия из `RhythmLevelEditorWindow.DrawTimelineBig` но упрощённая):
  - Волна трека (сгенерируй `waveformCache` как в эдиторе)
  - Сетка 4б белая / 1б серый
  - Кнопка `+ Нота` — ставит `HIT` на текущий `previewTime` (квант 0.5) и сразу открывает выбор `Префаб 0..6` (круглые кнопки с цветом)
  - Перетаскивание ХИТа мышкой, `Del` — удалить, `Квантовать`
  - Ползунок времени (огромный, 18px) + `⏮/◀/▶/Loop`
- **Низ:** `Сохранить` → `JsonUtility.ToJson(levelData)` → `File.WriteAllText(Application.persistentDataPath + "/Levels/"+fullTitle+".json", json)` + `AssetDatabase` в эдиторе → `CreateAsset`. `Запустить` → `RhythmParkourManager.levelData = thisData; SceneManager.LoadScene("IsGameScene");`

**Логика ХИТ:** как в эдиторе — игрок жмёт в **ХИТ** (когда у игрока), код `TravelForSpeed(speed)` по `spawn→hitTrigger` (52м/ speed), `spawn = hit - travel` — игра сама считает, невидимая стена `HitTrigger` (жёлтый куб, `isTrigger`, `6×3×1` на `Z≈8`) — ориентир, но игроку не надо его выбирать.

## 4. Превью и ползунок
- `Conductor.songPositionBeats` + `AudioSource.time` для слайдера.
- `Timeline` рисует волну + `playhead` (зелёный). Перемотка — `previewTime` + `AudioSource.time = previewTime`, `dspStart = AudioSettings.dspTime - previewTime`.
- `Update` препятствий — уже по `songPosition` (`spawnPosition + dir*speed*(songTime-spawnTime)`), поэтому превью точно.

## 5. Экспорт/Запуск
- **Сохранение:** `File.WriteAllText(path, JsonUtility.ToJson(levelData, true))` + `PlayerPrefs` для списка.
- **Загрузка в игре:** `RhythmParkourManager.PrepareLevel(loadedData)` + `Play()`. Для файла с диска — `JsonUtility.FromJson<RhythmLevelData>` нельзя (ScriptableObject), поэтому храни DTO `LevelSave { string fullTitle, songAuthor, mapAuthor, bpm, offset, coverBase64, List<EventSave> }` и конвертируй.
- **Запуск:** кнопка `Тест` — `Time.timeScale=1`, `manager.Play()`, `editorCanvas.SetActive(false)`, `Cursor.lockState=Locked`.

## 6. Что уже готово и что доделать
- Готово: `RhythmLevelData` (cover/fullTitle), `Obstacle` (скорость/урон/триггер-компенсация), `RhythmParkourManager` (авто HitTrigger, travel), `SphereBeatRotator` (TextMeshPro + DOTween), `FreeCamera.cs` (WASD+ПКМ+Q/E+Shift+колесо)
- Удалить: `AutoPlayController.cs` — уже удалён, `FirstPersonController` очищен от `autoPlay`
- Сделать: создать `LevelEditor.unity` по этому плану, скопировать UI из `RhythmLevelEditorWindow` (секции ①②③) в `Canvas`, подключить `InGameEditorController.cs` (логика выше).

Хочешь — могу сгенерить `InGameEditorController.cs` заготовку и префаб `LevelEditorCanvas` в один клик.
