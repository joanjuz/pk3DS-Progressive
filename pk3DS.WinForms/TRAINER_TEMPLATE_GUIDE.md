# Trainer Randomizer Templates

Esta versión agrega plantillas reutilizables al randomizador de entrenadores de Gen 6 y Gen 7.

## Uso recomendado

1. Abra el randomizador de entrenadores.
2. Configure normalmente:
   - Progressive BST y sus rangos.
   - Opciones globales de movimientos.
   - Level Caps y los entrenadores seleccionados.
   - Move Rules por entrenador.
3. Pulse `Template...` -> `Save current template...`.
4. En otra ROM/sesión del mismo juego pulse `Template...` -> `Load template...`.

La plantilla guarda el código del juego (`XY`, `ORAS`, `SM` o `USUM`). Si intenta cargar una plantilla de otro juego, pk3DS la rechaza para evitar que IDs de entrenadores de un juego se apliquen a otro.

## Ubicación de las plantillas

Las plantillas de entrenadores se guardan y cargan desde la misma carpeta `custom_balance_templates` que ya usa pk3DS para `moves_gen6.csv`, `moves_gen7.csv` y las plantillas de evoluciones. El diálogo `Template...` abre esa carpeta por defecto.

## Qué guarda

### Progressive BST
- Activado/desactivado.
- Min Level / Max Level.
- Min BST / Max BST.
- Full Random.

### Move Settings globales
- Fuente de movimientos: `DontModify`, `RandomizeAll`, `LevelUpOnly`, `Metronome`.
- Better Movesets.
- Force High Power y nivel.
- No Fixed Damage.
- Ensure Damaging Moves y cantidad.
- Ensure STAB Moves y cantidad.

### Level Caps
- Level Caps activado/desactivado.
- Scale regular trainers toward the next cap.
- Gap previo al cap.
- Entrenadores seleccionados por `TrainerID`.
- Cap de cada entrenador.
- Mega y Z-Move por entrenador.

### Trainer Move Rules
- Use.
- Min Move Power.
- Strong Stat.
- Mixed Tolerance.
- Allow Status Moves.
- Better Movesets.
- Smart Items.
- EV override.

## Nota sobre `resetUnlistedTrainers`

Las plantillas guardadas desde la interfaz usan `resetUnlistedTrainers: true`. Esto significa que al cargar la plantilla se desmarcan primero los entrenadores no incluidos y luego se marcan solamente los que aparecen en el JSON.

Si edita el JSON manualmente y pone `resetUnlistedTrainers: false`, la plantilla solo modificará los IDs listados y dejará intactos los demás entrenadores.
