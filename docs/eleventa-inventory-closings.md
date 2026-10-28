# Cierres de inventario

Horario acordado: 22:35, America/Mexico_City, cada domingo y cada último día de mes. Una sola captura sirve cuando ambas fechas coinciden. El cierre mensual es el saldo inicial del siguiente mes; no se duplica ni se suma al saldo semanal.

El agente ajusta la espera del ciclo para despertar a la hora de cierre aunque no corresponda la captura horaria. Captura el inventario y los costos en Firebird de solo lectura y los encola localmente incluso si otros lotes esperan internet. Una marca durable evita repetir el cierre al reiniciar el agente en la misma fecha. No se crea una tarea Windows adicional: la tarea existente debe mantener el agente ejecutándose y la computadora encendida.

Si el agente inicia después de las 22:35 y antes de medianoche en una fecha de cierre, captura al iniciar y conserva la hora real. Si el día ya pasó, no usa el inventario del día siguiente para inventar un cierre. La ejecución depende de que Firebird esté disponible y de que la computadora esté encendida; suspenderla impide capturar durante la suspensión.

La API `GET /api/integrations/eleventa/v1/inventory/closings` requiere el token existente; admite `from` y `to` (fechas locales, hasta 366 días), por defecto los últimos 90 días. Selecciona la primera captura completa del mismo día desde las 22:35. Devuelve `complete` si llegó dentro de cinco minutos, `late` si se capturó después, `missing` sin captura y `missing-cost` cuando no todos los costos históricos son válidos. Usa la fecha de extracción, no la recepción: un cierre encolado sin internet puede confirmarse después. Las fechas anteriores a esta versión también pueden aparecer sin captura/costo; no se reconstruyen retrospectivamente.

El dashboard muestra cierres, su estado, fecha real y valor guardado. No calcula utilidad neta ni gastos.

## Despliegue

No requiere nuevas columnas en Supabase; usa el esquema de costos de `06_eleventa_inventory_cost.sql` ya instalado. Desplegar la API y el frontend y luego actualizar únicamente el ejecutable del agente, finalizando y ejecutando su tarea del Programador de tareas. Conservar configuración y outbox. El agente anterior mantiene capturas por hora, pero no garantiza los cierres de las 22:35.

## Pruebas

El modo `--unit` comprueba límite de hora, zona México/UTC, coincidencia domingo/fin de mes, año bisiesto, reinicios, recuperación el mismo día y no reconstrucción al día siguiente. PostgreSQL aislado comprueba primera captura válida, cierre compartido, capturas tardías, exclusión de incompletas, costos desconocidos y cierres aún no vencidos.
