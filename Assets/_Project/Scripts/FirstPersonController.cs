using UnityEngine;

public class FirstPersonController : MonoBehaviour
{
    public float walkSpeed = 4f;
    public float mouseSensitivity = 2f;
    public float gravity = -9.81f;

    private CharacterController cc;
    private Camera cam;
    private Vector3 velocity;
    private float xRotation = 0f;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // 鼠标视角
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;
        transform.Rotate(Vector3.up * mx);
        xRotation = Mathf.Clamp(xRotation - my, -90f, 90f);
        cam.transform.localRotation = Quaternion.Euler(xRotation, 0, 0);

        // WASD 移动
        Vector3 move = transform.right * Input.GetAxis("Horizontal")
                     + transform.forward * Input.GetAxis("Vertical");
        cc.Move(move * walkSpeed * Time.deltaTime);

        // 重力
        velocity.y += gravity * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
