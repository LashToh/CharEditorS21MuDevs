# MuBreda Editor

Editor de escritorio para un servidor **MuOnline Season 21** armado con archivos MuDevs. Sirve para revisar y modificar cuentas, personajes, inventario, baúl, guild, árboles de habilidades y artifacts sin abrir el juego.

La interfaz está en español. El programa es WinForms sobre .NET 10 y habla directo con SQL Server.

## Qué edita

- **Cuenta:** datos de `MEMB_INFO` y la lista de personajes.
- **Personaje:** nivel, stats, clase y su evolución, zen, puntos, master level y master points.
- **Inventario y baúl:** grilla con la imagen del ítem. Ctrl+click selecciona varios, Supr borra, Ctrl+Z deshace y Ctrl+G guarda el inventario.
- **Ítem:** nivel, skill, luck, option, excellent, ancient, legendary, harmony, sockets, pentagram y alas, con el nombre y las líneas como en el cliente.
- **Sets:** arma un ancient o un legendary con las piezas de `SetItemType`.
- **Habilidades:** lista de aprendidas, árbol master, árbol enhance (4ª y 5ª) y pasivas, con los puntos de cada uno.
- **Artifact:** tablero de hexágonos, tipo, mejora +0 a +15 y opciones.
- **Guild:** datos del clan y de los miembros.

El guardado del personaje, del inventario, de las habilidades y del artifact exige que la cuenta esté desconectada.

## Atajos

| Tecla | Dónde | Qué hace |
| --- | --- | --- |
| Ctrl+click | Inventario y baúl | Suma o saca el slot de la selección |
| Supr | Inventario y baúl | Borra los ítems seleccionados |
| Ctrl+Z | Inventario y baúl | Deshace el último cambio |
| Ctrl+G | Inventario | Guarda el inventario |

En el árbol, la rueda cambia el nivel de la skill, el doble click la pone al máximo y el click derecho la pone en 0.

## Datos que usa

La conexión y las carpetas se leen de `MuBredaEditor.ini` (menú Archivo → Conexión y carpetas). Ese archivo no se sube al repositorio.

- **SQL Server:** base `MuOnline`. Inventario en `Character.Inventory` (239 slots de 25 bytes). Baúl en `warehouse.Items`.
- **Imágenes de ítems:** `Item\<categoría>\<índice>.png`, las mismas que usa el editor de ítems.
- **Íconos de skills:** hojas del cliente en `Data\Interface` (`newui_skill*.OZJ`, `new_Master_Icon*.OZJ`, `4th_Skill_Icon.OZJ`, `5th_Skill_Icon.OZJ`, `AbilitySkill_5th_Icon.OZJ`). El número de ícono sale de `Skill(Kor).txt`.
- **Tablas del servidor:** `Skill.xml`, `MasterSkillTree.xml`, `EnhanceSkillTree.xml` y `ArtifactSystem.xml`.

## Cómo correrlo

Hace falta el SDK de .NET 10. Desde esta carpeta:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o dist
```

El ejecutable queda en `dist\MuBredaEditor.exe`. Hay que cerrarlo y volver a abrirlo para ver un publish nuevo. `dist\` no se commitea.

## Límites conocidos

- El mapa `Spider_Slot` del cliente no está en los archivos del servidor. El tablero del artifact numera las casillas en espiral desde el centro. El slot que se guarda es ese número.
- No hay filas de artifact creadas por el servidor para copiar el formato. Las opciones se escriben en los bytes de socket del ítem.
- Las opciones de grado de las alas de 5ª no van en los sockets: si se escriben ahí, el wing core no entra.
