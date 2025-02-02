using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Windows.Kinect;
using System.Threading.Tasks; // Hinzugefügt für Parallel

public class PlayerMovement_v2 : MonoBehaviour
{

    Rigidbody rb;
    [SerializeField] float LRmovementSpeed = 5f;
    [SerializeField] float VHmovementSpeed = 0f;                     // Vorne / Hinten Movement Speed
    [SerializeField] float jumpForce = 5f;

    [SerializeField] Transform groundCheck;
    [SerializeField] LayerMask ground;
    [SerializeField]
    public GameObject playerObject;

    private KinectSensor _sensor;
    private BodyFrameReader _reader;
    private Body[] _Data;

    // Variablen fuer die Gelenkpositionen - speichert immer die vorherigen Positionen
    private float _previousXPosition;
    private float _previousYPositionBase;
    private float _previousHeadPositionY; 
    private float _previousKneeLeftPositionY;
    private float _previousKneeRightPositionY;

    // Schwellen fuer die Positionsaenderung - umso gruesser bzw. kleiner diese sind, desto schwieriger
    private float MovementThreshold = 0.09f;     // Schwelle fuer Seitwaertsbewegung
    private float BendingThreshold = -0.2f;     // Schwelle fuer das Buecken (negative Werte, da Y-Position nach unten zunimmt)
    public float JumpThreshold = 0.015f;          // Schwelle fuer das Erkennen eines Sprungs

    // Variablen fuer den aktuellen Zustand
    public static bool isJumping = false;
    public static bool isMovingLeft = false;
    public static bool isMovingRight = false;
    public static bool isDucking = false; 

    // Definiere die Gelenktypen fuer den SpineBase (Rumpf), Kopf, linken Fuss und rechten Fuss
    JointType spineBase = JointType.SpineBase;
    JointType head = JointType.Head;
    JointType kneeLeft = JointType.KneeLeft;
    JointType kneeRight = JointType.KneeRight;

    // Start wird beim ersten Frame aufgerufen
    void Start()
    {
        rb = GetComponent<Rigidbody>();

        _sensor = KinectSensor.GetDefault();

        if (_sensor != null)
        {
            _reader = _sensor.BodyFrameSource.OpenReader();

            if (!_sensor.IsOpen)
            {
                _sensor.Open();
            }
        }

        _Data = new Body[_sensor.BodyFrameSource.BodyCount];
    }

    // Update wird einmal pro Frame aufgerufen
    /*void Update()
    {
        //float horizontalInput = Input.GetAxis("Horizontal");
        //float verticalInput = Input.GetAxis("Vertical");
        // Preserve the vertical velocity
        float verticalVelocity = rb.velocity.y;

        if (_reader != null)
        {
            var frame = _reader.AcquireLatestFrame();

            if (frame != null)
            {
                frame.GetAndRefreshBodyData(_Data);

                foreach (var body in _Data)
                {
                    if (body != null && body.IsTracked)
                    {
                        horizontalMovement(body);
                        verticalMovement(body);
                    }
                }

                frame.Dispose();
            }
        }*/
    void Update()
    {
        float verticalVelocity = rb.velocity.y;

        if (_reader != null)
        {
            var frame = _reader.AcquireLatestFrame();

            if (frame != null)
            {
                frame.GetAndRefreshBodyData(_Data);

                // Parallelisiere die Verarbeitung der Body-Daten
                Parallel.ForEach(_Data, body =>
                {
                    if (body != null && body.IsTracked)
                    {
                        // Horizontale und vertikale Bewegung parallel verarbeiten
                        Parallel.Invoke(() => horizontalMovement(body), () => verticalMovement(body));
                    }
                });

                frame.Dispose();
            }
        }

        // Reset horizontal velocity when neither left nor right key is pressed
        if ((!Input.GetKey("left") && !Input.GetKey("right")) || (!isMovingRight && !isMovingLeft))
        {
            rb.velocity = new Vector3(rb.velocity.x * 0.9f, verticalVelocity, VHmovementSpeed);
        }

       // rb.velocity = new Vector3(rb.velocity.x, rb.velocity.y, VHmovementSpeed);
        
        if(Input.GetKey("left") || isMovingLeft)
        {
            GoLeft();
        }

        if(Input.GetKey("right") || isMovingRight)
        {
            GoRight();
        }

        if ((Input.GetButtonDown("Jump") || isJumping) && IsGrounded())
        {
            Jump();
        }

    }
    
    void GoLeft()
    {
        rb.velocity = new Vector3(LRmovementSpeed * -1, rb.velocity.y, rb.velocity.z);
        /*
        if (IsGrounded())
        {
            playerObject.GetComponent<Animator>().Play("Right Strafe");     //Adds animation for moving right
        }
        */
        
    }

    void GoRight()
    {
        rb.velocity = new Vector3(LRmovementSpeed, rb.velocity.y, rb.velocity.z);
        /*
        if (IsGrounded())
        {
            playerObject.GetComponent<Animator>().Play("Left Strafe");        //Adds animation for moving right
        }
        */
        
    }
    void Jump()
    {
        rb.velocity = new Vector3(rb.velocity.x, jumpForce, rb.velocity.z);
        playerObject.GetComponent<Animator>().Play("Jump");
    }


    bool IsGrounded()
    {
       return Physics.CheckSphere(groundCheck.position, .1f, ground);
    } 


   // Ueberwacht die links und rechts Bewegung
    void horizontalMovement(Body body)
    {
        // Erhalte die Positionen des Gelenkes des Koerpers aus dem Kinect-Body-Objekt
        CameraSpacePoint basePosition = body.Joints[spineBase].Position;
        // aktuelle X-Position des Gelenks
        float currentXPosition = basePosition.X;

         // Ueberpruefen ob die Aenderung der X-Position groeser ist als der Schwellenwert fuer seitliche Bewegungen
        if (Mathf.Abs(currentXPosition - _previousXPosition) > MovementThreshold)
        {
            // Falls die X-Position zunimmt, setze den Bewegungsstatus nach rechts.
            if (currentXPosition - _previousXPosition > 0)
            {
                isMovingRight = true;
                isMovingLeft = false;
                Debug.Log("Bewegt sich nach rechts------------------------------------------!");
            }
            // Falls die X-Position abnimmt, setze den Bewegungsstatus nach links.
            else
            {
                isMovingLeft = true;
                isMovingRight = false;
                Debug.Log("Bewegt sich nach links----");
            }
        }
        // Wenn die Aenderung der X-Position unter dem Schwellenwert liegt, setze beide Bewegungsstatus zurueck.
        else
        {
            isMovingLeft = false;
            isMovingRight = false;
        }

        _previousXPosition = currentXPosition;
    }

    /// Ueberwacht das Springen und Ducken
    void verticalMovement(Body body)
    {
       
        // Erhalte die Positionen der Gelenke des Koerpers aus dem Kinect-Body-Objekt
        CameraSpacePoint basePosition = body.Joints[spineBase].Position;
        CameraSpacePoint headPosition = body.Joints[head].Position;
        CameraSpacePoint kneeLeftPosition = body.Joints[kneeLeft].Position;
        CameraSpacePoint kneeRightPosition = body.Joints[kneeRight].Position;

        // aktuelle Y-Positionen der verschiedenen Gelenke
        float currentYPositionBase = basePosition.Y;
        float currentYPositionHead = headPosition.Y;
        float currentYPositionKneeLeft = kneeLeftPosition.Y;
        float currentYPositionKneeRight = kneeRightPosition.Y;

        // Variablen fuer die Differenzen der Positionen
        float baseChange = Mathf.Abs(currentYPositionBase - _previousYPositionBase);
        float headChange = currentYPositionHead - _previousHeadPositionY;
        float kneeLeftChange = Mathf.Abs(currentYPositionKneeLeft - _previousKneeLeftPositionY);
        float kneeRightChange = Mathf.Abs(currentYPositionKneeRight - _previousKneeRightPositionY);

        // Ueberprueft ob sich die Y-Position des SpineBase-Gelenks oder der Fuesse ausreichend aendert
        if (baseChange > JumpThreshold &&
            kneeLeftChange > 0.05f &&
            kneeRightChange > 0.05f )
        {
            // Hier wird der Sprungstatus geaendert
            isJumping = true;
            isDucking = false;
            Debug.Log("Springt------------------------------------------------------------");
        }
        // Ueberpruefe, ob sich die Y-Position des Kopfes nach unten aendert
        else if (headChange < BendingThreshold )
        {
            // Hier wird der Ducken-Status geaendert
            isDucking = true;
            isJumping = false;
            Debug.Log("Duckt--");
        }
        else
        {
            // Wenn keine der Bedingungen erfuellt ist, werden beide Status zurueckgesetzt
            isJumping = false;
            isDucking = false;
        }

        // Speichere die aktuellen Y-Positionen fuer den naechsten Frame
        _previousYPositionBase = currentYPositionBase;
        _previousHeadPositionY = currentYPositionHead;
        _previousKneeLeftPositionY = currentYPositionKneeLeft;
        _previousKneeRightPositionY = currentYPositionKneeRight;
    }

    // Wird aufgerufen, wenn die Anwendung beendet wird
    void OnApplicationQuit()
    {
        if (_reader != null)
        {
            _reader.Dispose();
            _reader = null;
        }

        if (_sensor != null)
        {
            if (_sensor.IsOpen)
            {
                _sensor.Close();
            }
        }
    }
}