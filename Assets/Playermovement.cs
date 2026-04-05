using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    CharacterController controller;
    public float walkSpeed = 5f;       // скорость ходьбы
    public float runSpeed = 10f;       // скорость бега
    public float crouchSpeed = 2.5f;   // скорость приседания
    public float slideSpeed = 18f;     // максимальная скорость скольжения
    public float slideDuration = 2f;   // длительность скольжения
    public float mouseSensitivity = 2f; // чувствительность мыши
    public float gravity = 18f;        // гравитация (сильная для воздушного контроля)
    public float jumpForce = 8f;       // сила прыжка
    public float wallBounceForce = 0.5f; // потри вертикальная (практически стопват фалл)
    public float wallBounceSpeed = 2000f; // Сильный плавный отскок

    float verticalRotation = 0f;       // для вертикального вращения камеры
    float horizontalRotation = 0f;     // для горизонтального вращения камеры
    float verticalVelocity = 0f;       // вертикальная скорость
    Vector3 horizontalVelocity = Vector3.zero; // горизонтальная скорость (для отскока)
    int jumpCount = 0;                 // счетчик прыжков
    public int maxJumps = 2;           // максимум прыжков
    bool isCrouching = false;          // приседает ли
    bool isSliding = false;            // скользит ли
    float originalHeight;              // оригинальная высота контроллера
    float slideTimer = 0f;             // таймер скольжения
    bool slideOnCooldown = false;      // кулдаун скольжения
    float wallBounceTimer = 0f;        // таймер кулдауна отскока от стены

    void Start()
    {
        controller = GetComponent<CharacterController>();
        originalHeight = controller.height; // сохраняем оригинальную высоту
        Cursor.lockState = CursorLockMode.Locked; // блокируем курсор для мыши
    }

    void Update()
    {
        // движение WASD относительно камеры
        float moveX = Input.GetAxis("Horizontal"); // A/D
        float moveZ = Input.GetAxis("Vertical");   // W/S

        Vector3 move = Camera.main.transform.right * moveX + Camera.main.transform.forward * moveZ;

        // определение скорости: Shift для бега, Ctrl для приседания/скольжения
        float currentSpeed = walkSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            currentSpeed = runSpeed; // бег
        }

        // приседание или скольжение с Ctrl
        if (Input.GetKey(KeyCode.LeftControl))
        {
            // скольжение когда бежим
            bool isRunning = Input.GetKey(KeyCode.LeftShift);
            if (isRunning && !slideOnCooldown && controller.isGrounded)
            {
                // начинаем скольжение при беге
                if (!isSliding)
                {
                    isSliding = true;
                    slideTimer = slideDuration;
                    controller.height = originalHeight / 2;
                }
            }
            
            if (isSliding && slideTimer > 0)
            {
                // скольжение с постепенным замедлением
                float slideProgress = slideTimer / slideDuration; // 1.0 в начале, 0.0 в конце
                currentSpeed = slideSpeed * slideProgress; // убывает от slideSpeed к 0
                slideTimer -= Time.deltaTime;
            }
            else if (isSliding && slideTimer <= 0)
            {
                // конец скольжения
                isSliding = false;
                slideOnCooldown = true;
                controller.height = originalHeight;
            }
            else if (!isSliding && !isRunning)
            {
                // приседание при ходьбе (не скольжение)
                if (!isCrouching)
                {
                    isCrouching = true;
                    controller.height = originalHeight / 2;
                }
                currentSpeed = crouchSpeed; // медленнее при приседании
            }
        }
        else
        {
            // сбрасываем приседание и скольжение при отпускании Ctrl
            if (isCrouching || isSliding)
            {
                isCrouching = false;
                isSliding = false;
                controller.height = originalHeight;
            }
        }

        // сбрасываем кулдаун скольжения при беге
        if (Input.GetKey(KeyCode.LeftShift) && !isSliding)
        {
            slideOnCooldown = false;
        }

        move = move * currentSpeed;
        move += horizontalVelocity; // добавляем горизонтальную скорость отскока
        
        // замедляем горизонтальную скорость медленнее (дольше летим в сторону)
        horizontalVelocity *= 0.999f;

        // прыжок и двойной прыжок
        wallBounceTimer -= Time.deltaTime; // уменьшаем таймер кулдауна
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // проверяем стену для отскока если в воздухе (не считается за прыжок)
            if (!controller.isGrounded)
            {
                RaycastHit wallHit;
                if (wallBounceTimer <= 0 && WallCheck(out wallHit))
                {
                    // отскок от стены (не использует прыжок!)
                    Debug.Log($"Отскок от стены! Нормаль: {wallHit.normal}, Точка: {wallHit.point}, Направление отскока: {wallHit.normal}, Скорость: {wallBounceSpeed}");
                    verticalVelocity = jumpForce * 0.7f;  // Небольшой подъём вверх, чтобы немного взлететь
                    Vector3 bounceDir = wallHit.normal;  // Направление от стены (перпендикулярно)
                    horizontalVelocity = bounceDir * wallBounceSpeed; // сильный толчок от стены
                    wallBounceTimer = 0.5f; // кулдаун 0.5 секунды
                    Debug.Log($"Установлена horizontalVelocity: {horizontalVelocity}");
                }
                else if (jumpCount < maxJumps)
                {
                    // обычный двойной прыжок если нет стены
                    verticalVelocity = jumpForce;
                    jumpCount++;
                }
            }
            else if (jumpCount < maxJumps)
            {
                // обычный прыжок с земли
                verticalVelocity = jumpForce;
                jumpCount++;
            }
        }

        // гравитация
        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f; // небольшой отрицательный для удержания на земле
            jumpCount = 0; // сбрасываем прыжки на земле
        }
        verticalVelocity -= gravity * Time.deltaTime; // всегда полная гравитация
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);
        Debug.Log($"Финальный вектор движения: {move}, Time.deltaTime: {Time.deltaTime}, Применённое движение: {move * Time.deltaTime}");

        // мышь для поворота камеры
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        horizontalRotation += mouseX;
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -60f, 60f);

        Camera.main.transform.rotation = Quaternion.Euler(verticalRotation, horizontalRotation, 0f);
    }

    bool WallCheck(out RaycastHit wallHit)
    {
        // проверка стен спереди (в направлении взгляда камеры)
        Vector3 forwardDir = Camera.main.transform.forward;
        if (Physics.Raycast(transform.position, forwardDir, out wallHit, 1f))
        {
            Debug.Log($"Попадание в стену спереди: {wallHit.point}");
            return true;
        }
        
        // проверка стен слева и справа
        if (Physics.Raycast(transform.position, transform.right, out wallHit, 1f))
        {
            Debug.Log($"Попадание в стену слева: {wallHit.point}");
            return true;
        }
        
        if (Physics.Raycast(transform.position, -transform.right, out wallHit, 1f))
        {
            Debug.Log($"Попадание в стену справа: {wallHit.point}");
            return true;
        }
        return false;
    }
}