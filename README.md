# Cloud Garden Tycoon

Juego **Tycoon / Idle 3D para Android** hecho en **Unity 6 (C#, URP)**. Empiezas con una isla flotante casi vacía, recoges rocío, construyes máquinas, encadenas producción, investigas tecnologías, desbloqueas cinco islas y acabas levantando una civilización industrial celestial.

- Paquete: `com.julianfarks.cloudgardentycoon` · Orientación: horizontal · Cámara: isométrica 3D táctil · 100 % offline.
- Sin assets externos: **todo el arte (modelos, texturas pintadas, iconos) se genera por código** al arrancar.

---

## Estado real del proyecto (léelo primero)

Este repositorio contiene el proyecto Unity completo, pero **todavía no se ha compilado ni ejecutado ninguna vez**. El entorno donde se escribió no tenía Unity ni .NET, y no se simuló ninguna compilación.

| Qué | Estado |
|---|---|
| Código C# (núcleo, visuales, UI, editor) | Escrito y revisado a mano. **Sin compilar.** Puede haber errores de compilación que solo saldrán al abrir el proyecto en Unity. |
| Pruebas automáticas (78 en `Assets/Tests/EditMode`) | Escritas. **Sin ejecutar.** Las ejecuta la CI (job `test`). |
| APK | **No existe todavía.** Lo genera la CI o tú en local (ver abajo). |
| GitHub Actions | Configurado (`.github/workflows/build.yml`). Necesita secretos de licencia Unity para ejecutarse. |
| Equilibrio económico | Calibrado con un simulador propio (`tools/economy_sim.py`) solo en las primeras ~5 h. Las islas 4 y 5 **no están verificadas** y la duración de 10-12 h es un objetivo de diseño, no una medición. |
| Aspecto visual | Diseñado sin poder verlo en pantalla. Espera retoques de colores, escalas y posiciones tras la primera ejecución. |

## Cómo jugar

1. Toca **Recolectar** para conseguir rocío y **Vender rocío** para convertirlo en monedas.
2. Con 15 monedas construye tu primer **Recolector de Rocío** (menú *Construir*). Arrastra el edificio fantasma o toca una casilla; verde = válido, rojo = ocupado.
3. Construye el **Mercado Flotante** y activa la venta automática (*Gestión → Ventas*).
4. Investiga (*Investigar*), encadena **Destiladora → Invernadero → Procesadora…**, amplía el terreno y desbloquea las demás islas (*Expandir*).
5. Construye el **Puerto Aéreo** para recibir pedidos de comerciantes (*Comercio*).
6. Al completar los 20 hitos de la campaña se desbloquea el **Renacimiento Celestial** (estrellas permanentes, mejoras y desafíos).

Controles: un dedo = mover cámara, pellizco = zoom, toque en edificio = seleccionar (mejorar / mover / retirar).

## Contenido

- **12 recursos**: rocío, agua, polen, viento, nubes, cristales, esencia, monedas, productos, fruta, hielo, energía. Cada uno con cantidad, capacidad, producción/s, consumo/s y valor comercial.
- **30 edificios** + el Colector Dorado (máquina de prestigio), 5 categorías, 10 niveles cada uno (el nivel 10 da bonificación ×1,5 y un aspecto especial; el modelo cambia en los niveles 4, 7 y 10).
- **46 investigaciones** con prerrequisitos, edificios requeridos y materiales.
- **5 regiones** (Jardín del Rocío, Isla Tropical, Isla Nevada, Isla Tormentosa, Isla Celestial), cada una con terreno, decoración, cielo y recursos propios, ampliables en 4 niveles.
- **Comercio**: pedidos generados según tu progresión, pedidos especiales, recompensas en monedas y recursos.
- **Guardado** automático con versión, hash de integridad, 2 copias de seguridad y recuperación ante archivos corruptos.
- **Producción sin conexión**: 2 h al inicio, hasta 8 h con investigación (12 h con el Renacimiento), protegida contra cambios de hora.
- **Estadísticas**, venta automática con reserva, 4 perfiles gráficos (Bajo/Medio/Alto/Ultra).

## Estructura del código

```
Assets/
  Scripts/
    Simulation/          # CloudGarden.Core  (C# puro, sin UnityEngine: 100 % testeable)
      Core/              # GameState (modelo y acciones del jugador)
      Economy/           # recursos, almacenes, formato de números
      Production/        # simulación central y modificadores
      Buildings/         # definiciones, niveles, base de datos
      Construction/      # cuadrícula e impedimento de solapes
      Islands/           # definición de regiones
      Research/          # árbol tecnológico
      Trading/           # pedidos de comerciantes
      Progression/       # campaña, estadísticas, prestigio, mejoras globales
      SaveSystem/        # DTOs, validación y reparación de partidas
      OfflineProduction/ # simulación sin conexión
    Game/                # CloudGarden.Game (Unity)
      Bootstrap/         # GameController: carga, bucle, guardado
      Islands/           # mallas, materiales, texturas, 31 modelos 3D procedurales, islas, nubes
      Construction/      # cámara isométrica, colocación con vista previa, selección
      Optimization/      # perfiles de calidad, object pooling
      SaveSystem/        # archivo atómico + copias de seguridad
      UI/                # HUD y paneles creados por código
  Editor/                # configuración del proyecto y script de build Android
  Tests/EditMode/        # pruebas NUnit
tools/economy_sim.py     # simulador de ritmo de la economía
```

Principios: el modelo (`GameState`) es la única fuente de verdad y notifica por eventos; la capa Unity solo lo refleja. Los datos (edificios, investigaciones, regiones) viven en tablas C# (`*Database.cs`).

## Abrir el proyecto

1. Instala **Unity 6** (6000.0 LTS) con el módulo **Android Build Support** (SDK, NDK y OpenJDK).
2. Abre la carpeta del repositorio con Unity Hub. La primera vez, `Assets/Editor/ProjectSetup.cs` crea automáticamente el *pipeline* URP, los materiales plantilla (`Assets/Resources/Materials`), la escena vacía `Assets/Scenes/Main.unity` y fija *Active Input Handling = Input Manager (Old)* (reinicia el editor si lo pide).
3. Abre `Assets/Scenes/Main.unity` y pulsa **Play**: `GameBootstrap` construye el mundo, la cámara y la interfaz desde código.

> Si Unity cambia de versión, actualiza `ProjectSettings/ProjectVersion.txt` y la versión de URP en `Packages/manifest.json` (`17.0.x` para Unity 6000.0).

## Pruebas

`Window → General → Test Runner → EditMode → Run All`, o en línea de comandos:

```bash
unity-editor -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults results.xml
```

Cubren: compra y colocación de edificios, solapes y límites, mover/retirar con reembolso, mejoras y niveles, producción por segundo (independiente del paso de tiempo), consumo, almacenes llenos, cadenas de fabricación, investigaciones, regiones, comercio, producción offline y antitrampas de reloj, guardado/carga (incluidos datos corruptos y copias de seguridad), prestigio, integridad de las tablas de datos y una prueba aleatoria de 3.000 acciones que comprueba que **ningún recurso puede ser negativo, NaN ni superar su capacidad**.

## Compilar el APK

### En local
```bash
unity-editor -batchmode -quit -projectPath . -buildTarget Android \
  -executeMethod CloudGarden.EditorTools.BuildScript.BuildAndroid \
  -customBuildPath build/Android -customBuildName CloudGardenTycoon -buildVersion 0.1.0 -androidVersionCode 1
```
Resultado: `build/Android/CloudGardenTycoon.apk`. También en el menú **Cloud Garden → Build Android APK**. Sin *keystore* propio, Unity lo firma con su clave de depuración (instalable en cualquier móvil; para Google Play necesitarás tu propia firma, que **nunca** debe subirse al repositorio).

### En GitHub Actions
`.github/workflows/build.yml` ejecuta en cada *push*: pruebas EditMode → compilación Android → verificación del APK (existe, no está vacío, contiene `AndroidManifest.xml`, SHA-256) → artefacto `CloudGardenTycoon-apk`.

**Configuración necesaria (una sola vez)** en *Settings → Secrets and variables → Actions*:

| Secreto | Valor |
|---|---|
| `UNITY_LICENSE` | contenido del archivo `.ulf` de tu licencia (ver [game.ci/docs/github/activation](https://game.ci/docs/github/activation)) |
| `UNITY_EMAIL`, `UNITY_PASSWORD` | alternativa para cuentas que lo permitan |

Sin esos secretos los trabajos de Unity se omiten con un aviso (no fallan).

### Release de prueba con el APK
- Crea una etiqueta: `git tag v0.1.0 && git push origin v0.1.0` → la CI compila y publica un *release* con el APK adjunto.
- O ejecuta el workflow a mano (*Actions → Build Android → Run workflow*) marcando **release** para publicar un *pre-release* de prueba.

## Economía y ritmo

- Las máquinas funcionan como un caudal continuo: cada una ejecuta la fracción de su ciclo que permiten sus entradas y el espacio libre del almacén (se detiene cuando está lleno y no consume si no puede producir).
- El coste de cada copia de una máquina crece de forma pronunciada (×1,25-×1,5), así que el avance viene de mejorar, ampliar y subir de nivel tecnológico, no de apilar copias.
- `python3 tools/economy_sim.py` ejecuta un bot voraz sobre las mismas tablas. Resultado con los valores actuales: segunda isla a ~1,5-2 h y tercera a ~4-5 h; el bot es torpe y nunca llega a las islas 4-5, por lo que **esa parte del ritmo es una estimación sin validar**. Hace falta jugar para afinarla.

## Pendiente / ideas

- Primera compilación y ejecución real: corregir los errores que aparezcan y revisar el aspecto visual en un móvil.
- Verificar rendimiento (60 FPS) en dispositivos reales y ajustar los perfiles de calidad.
- Equilibrar las islas 4 y 5 y la duración total con partidas reales.
- Sonido y música (no incluidos), tutorial guiado, localización a otros idiomas.
- Firma de release y publicación en Google Play.
