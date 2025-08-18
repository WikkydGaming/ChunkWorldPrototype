using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class CharacterHeightFollower : MonoBehaviour
{
    public float moveSpeed = 6f;
    public float heightSmooth = 20f;      // how fast Y lerps to ground
    public float eyeHeight = 1.7f;        // above ground

    CharacterController cc;

    void Awake() { cc = GetComponent<CharacterController>(); }

    void Update()
    {
        // Simple WASD movement on XZ plane
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dir = new Vector3(h, 0, v).normalized;
        Vector3 deltaXZ = dir * moveSpeed * Time.deltaTime;

        // Move horizontally
        cc.Move(new Vector3(deltaXZ.x, 0f, deltaXZ.z));

        // Sample ground height from procedural function (no collider needed)
        float groundY = SampleGround(transform.position.x, transform.position.z);

        // Smoothly settle to ground + eye height
        float targetY = groundY + eyeHeight;
        Vector3 p = transform.position;
        p.y = Mathf.Lerp(p.y, targetY, heightSmooth * Time.deltaTime);
        transform.position = p;
    }

    float SampleGround(float wx, float wz)
    {
        int gx = Mathf.FloorToInt(wx / ChunkMath.TILE_SIZE);
        int gy = Mathf.FloorToInt(wz / ChunkMath.TILE_SIZE);
        return WorldGen.GetHeightM(gx, gy);
    }
}
