using System.Collections;
using UnityEngine;

namespace Lalim.UI
{
    public class LevelIntroUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _displayDuration = 3f;
        [SerializeField] private float _fadeDuration = 1f;

        private void Start()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }

            if (_canvasGroup != null)
            {
                StartCoroutine(DisplayAndFadeOut());
            }
        }

        private IEnumerator DisplayAndFadeOut()
        {
            _canvasGroup.alpha = 1f;
            yield return new WaitForSeconds(_displayDuration);

            float t = 0f;
            while (t < _fadeDuration)
            {
                t += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / _fadeDuration);
                yield return null;
            }

            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }
    }
}
