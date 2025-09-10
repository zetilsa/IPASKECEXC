using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public bool Visible;
    [SerializeField]
    public Transform[] pos;
    public Transform SelectedUI;
    public int selected;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            Visible = !Visible;
            UpdatePanel();
        }
        if (Input.GetAxis("Mouse ScrollWheel") != 0)
        {
            selected -= (int)Input.GetAxis("Mouse ScrollWheel");

            if (selected < 0)
            {
                selected = 6;
            }
            else if (selected > 6)
            {
                selected = 0;
            }
            UpdatePosition();
        }
    }



    void UpdatePanel()
    {
        if (Visible == true)
        {

        }
        else if (Visible == false)
        {

        }
    }

    void UpdatePosition()
    {
        SelectedUI.localPosition = pos[selected].localPosition;
    }
}
