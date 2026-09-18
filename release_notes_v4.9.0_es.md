# pk3DS-Progressive v4.9.0 Stable

## Cambios principales

Esta versión marca una actualización importante desde v4.8.1 e integra las funciones desarrolladas y probadas en la rama Eris-Ultranova.

### Trainer Randomizer

- Mejoras generales en la interfaz del Trainer Randomizer.
- Progressive BST para controlar la fuerza de los Pokémon según el nivel.
- Level Caps configurables.
- Trainer Move Rules.
- Better Movesets.
- Smart Held Items y mejoras relacionadas con objetos de entrenadores.
- Opción para convertir combates seleccionados en Double Battles mediante porcentaje configurable.
- Mejor organización de las opciones de dificultad y randomización.
- Soporte para templates de configuración de entrenadores.

### Pokémon X/Y y Omega Ruby / Alpha Sapphire

- Nuevo sistema de Catch Zones basado en parches JSON.
- Ya no es necesario distribuir archivos completos modificados del juego para Catch Zones.
- Mejoras en randomización de entrenadores, objetos y otras funciones de Gen 6.
- Ajustes de interfaz y compatibilidad en varios editores.

### Pokémon Sun / Moon / Ultra Sun / Ultra Moon

- Mejoras en Static Encounter Editor.
- Mejoras en Move Tutor Editor.
- Opción para hacer gratuitos los Move Tutors en USUM.
- Mejoras en Mart Editor.
- Mejoras en Field Item Randomizer.
- Mejoras en randomización de entrenadores, encuentros y objetos.

### Ultra Sun / Ultra Moon

- Nuevo parche para convertir NPCs de Pokémon Center Café en Move Relearner.
- El Move Relearner solamente muestra movimientos que el Pokémon puede aprender a su nivel actual o a niveles anteriores.
- Nueva opción para que los intercambios con NPC acepten cualquier Pokémon.
- El sistema modifica tanto la especie solicitada como la lógica de selección del juego para que "(Any Pokemon)" funcione realmente como comodín.
- Soporte para randomizar el Pokémon entregado por los NPC en los intercambios.

### Custom Battle Patches

- Integración de parches personalizados para modificar efectos de movimientos en Gen 7.
- Soporte mediante identificadores BattlePatch dentro de los templates de movimientos.
- Se incluyen las modificaciones y correcciones desarrolladas para movimientos especiales compatibles con el sistema.

### Templates y balance personalizado

- Mejoras en Custom Balance Templates.
- Templates para movimientos, evoluciones y estadísticas.
- Mejoras en templates de entrenadores.
- Mejor manejo y validación de reglas personalizadas.

### Interfaz y calidad de vida

- Renovación de varias partes de la interfaz.
- Mejor distribución de controles y opciones.
- Limpieza de botones o funciones que todavía no están implementadas.
- Mejoras generales de estabilidad y manejo de configuraciones.

### Limpieza del proyecto

- Se eliminaron archivos locales que no deben formar parte del repositorio, incluyendo exefs.bin y randsettings.txt.
- Se mejoró .gitignore para evitar incluir archivos extraídos del juego, ROMs, backups, builds y archivos temporales.
- Correcciones de texto y codificación.
- Catch Zones ya no depende de distribuir archivos completos derivados del juego.

## Pruebas

La versión Stable fue probada en las rutas principales utilizadas por:

- Pokémon X/Y
- Pokémon Omega Ruby / Alpha Sapphire
- Pokémon Sun / Moon
- Pokémon Ultra Sun / Ultra Moon

Se verificaron apertura de editores, randomización, guardado, rebuild y ejecución de las funciones principales.

Las nuevas funciones de USUM Move Relearner y NPC Trades fueron verificadas directamente dentro del juego.

## Nota

pk3DS-Progressive es un proyecto independiente basado en pk3DS y se distribuye bajo la licencia GNU GPLv3.

No incluye ROMs, archivos completos extraídos de los juegos ni otros contenidos propiedad de Nintendo, Game Freak o The Pokémon Company.