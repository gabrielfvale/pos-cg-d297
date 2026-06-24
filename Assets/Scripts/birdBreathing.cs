using System.Collections;
using UnityEngine;

/// <summary>
/// Simula respiração animando a escala Y do personagem suavemente.
/// Funciona independente do BirdAnimator.
/// </summary>
public class BirdBreathing : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Intensidade")]
    [Tooltip("Quanto a escala Y expande no pico da respiração")]
    public float scaleAmount = 0.04f;

    [Header("Velocidade")]
    [Tooltip("Ciclos de respiração por minuto")]
    public float breathsPerMinute = 16f;

    [Header("Pivot")]
    [Tooltip("Anima também a posição Y para simular expansão a partir da base")]
    public bool offsetPosition = true;
    [Tooltip("Quanto sobe no pico (em unidades do mundo). Ajuste conforme o pivot do sprite)")]
    public float positionOffset = 0.02f;

    // ─── Privado ──────────────────────────────────────────────────────────────

    private Vector3   _baseScale;
    private Vector3   _basePosition;
    private Coroutine _breathCoroutine;

    // ─── Unity ────────────────────────────────────────────────────────────────

    void Awake()
    {
        _baseScale    = transform.localScale;
        _basePosition = transform.localPosition;
    }

    void OnEnable()
    {
        _breathCoroutine = StartCoroutine(BreathLoop());
    }

    void OnDisable()
    {
        if (_breathCoroutine != null) StopCoroutine(_breathCoroutine);
        transform.localScale    = _baseScale;
        transform.localPosition = _basePosition;
    }

    // ─── API pública ──────────────────────────────────────────────────────────

    /// <summary>Pausa a respiração e volta à pose base.</summary>
    public void StopBreathing()
    {
        enabled = false;
    }

    /// <summary>Retoma a respiração.</summary>
    public void StartBreathing()
    {
        enabled = true;
    }

    // ─── Coroutine ────────────────────────────────────────────────────────────

    private IEnumerator BreathLoop()
    {
        float period = 60f / Mathf.Max(breathsPerMinute, 0.1f); // segundos por ciclo

        while (true)
        {
            float elapsed = 0f;

            while (elapsed < period)
            {
                // seno suave: 0 → 1 → 0 ao longo de um ciclo
                float t = Mathf.Sin((elapsed / period) * Mathf.PI);

                transform.localScale = new Vector3(
                    _baseScale.x,
                    _baseScale.y + scaleAmount * t,
                    _baseScale.z
                );

                if (offsetPosition)
                {
                    transform.localPosition = new Vector3(
                        _basePosition.x,
                        _basePosition.y + positionOffset * t,
                        _basePosition.z
                    );
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // garante retorno exato à base ao fim do ciclo
            transform.localScale    = _baseScale;
            transform.localPosition = _basePosition;
        }
    }
}