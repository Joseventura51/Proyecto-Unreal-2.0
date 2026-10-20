# SENATI Map — Pirámide, simulación de tres niveles basada parcialmente en EV-52

## Evidencia visual recibida

Se utilizó una fotografía del plano de seguridad **SENATI sede Independencia — Evacuación 68_Dirección Nacional, primer piso, plano EV-52**.
La referencia no es un plano CAD legible y está tomada en perspectiva. Por tanto:
- La ubicación general de las franjas de salas, el corredor central y las dos escaleras se **aproxima a la fotografía**.
- Los límites exactos de muros, puertas, escala, nombres de ambientes y dimensiones no están verificados.
- Las líneas **rojas y verdes** de la fotografía son **recorridos de evacuación** y NO equivalen a caminos de navegación cotidiana ni a rutas de seguridad certificadas. El botón «Mostrar evacuación» exhibe exclusivamente un **esquema aproximado**, oculto por defecto.
- El ascensor de la simulación **no está identificado con certeza en la fotografía**. Se conserva como objeto **hipotético**, no como instalación confirmada.

## Diseño implementado

**Piso 1 (rediseñado según EV-52):**
- Cinco ambientes grandes en la franja superior; cuatro espacios en la inferior, numerados provisionalmente A-E / A-D.
- Corredor longitudinal de distribución, dependencias de apoyo a oeste y este, acceso conceptual desde el oeste, y dos sectores «Escalera 1 · oeste» y «Escalera 2 · este».
- Aproximación isométrica de mobiliario tipo aula, despachos, baños, escaleras y dependencias.
- Rutas de demostración que recorren la franja del pasillo y acceden a la habitación seleccionada por un punto de puerta **estimado**, no inspeccionado.

**Pisos 2 y 3:** conservan las distribuciones y muebles **conceptuales** de la versión 2.9.0; **no** se infieren del plano EV-52, que solo representa primer piso.

## Cómo probar
1. En **Pirámide → Piso 1** alterna vista isométrica y de planta, selecciona «Ambiente superior B» o «Ambiente inferior B» y pulsa «Ver ruta».
2. Pulsa «Mostrar evacuación» para ver los trazos rojo y verde esquemáticos. No utilizarlos en una emergencia.
3. Selecciona Piso 2 o Piso 3 para explorar las maquetas conceptuales.
4. Selecciona ambiente de piso 3, elige **Escaleras** o **Ascensor (simulación)** y pulsa «▶ Simular recorrido». El avatar parte del acceso conceptual del primer piso y cambia de planta.
5. Usa «Guardar piso» para conservar selección, perspectiva, zoom, piso y trazos visibles en Preferences de .NET MAUI.

## Validación técnica
El workflow de GitHub Actions comprueba:
- Sintaxis del JavaScript integrado y grafo QGIS del campus.
- Todas las **41 salas/áreas ilustrativas** (17 piso 1, 12 piso 2, 12 piso 3) y sus rutas entre plantas.
- Renderizado SVG del piso 1 (incluyendo evacuación opcional) y pisos 2 y 3 para impedir regresiones por diferencias de contorno.

## Pendiente antes de cartografía de interiores real
- Obtener plano vectorial o fotografía frontal de mejor calidad con cotas verificables, puertas y escala.
- Confirmar los nombres verdaderos de aulas y dependencias, la posición de Escalera 1 y 2, accesos y ascensores si existen.
- Levantar mapas independientes de segundo y tercer piso y asociarlos mediante escaleras/ascensor validados.
- Revisar físicamente los recorridos y señalética antes de habilitar indicaciones de navegación para estudiantes.

**Este módulo es una maqueta SVG isométrica (2.5D), no un modelo BIM/Blender ni un plano de evacuación habilitado.**
