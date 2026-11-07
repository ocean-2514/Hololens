using MixedReality.Toolkit;
using UnityEngine;

public class ButtonAction : MonoBehaviour
{
    [SerializeField] private StatefulInteractable button;

    private void OnEnable()
    {
        if (button != null)
            button.OnClicked.AddListener(HandleClick);
    }

    private void OnDisable()
    {
        if (button != null)
            button.OnClicked.RemoveListener(HandleClick);
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void HandleClick()
    {
        Debug.Log("button pressed!!");
    }
}
