import { useRef, useState } from 'react';
import { useAuth } from '../provider/AuthProvider';

const RegisterForm = () => {


    const [validationErrors, setValidationErrors] = useState({});

    const userNameRef = useRef();
    const emailRef = useRef();
    const passwordRef = useRef();
    const confirmPasswordRef = useRef();
    const firstNameRef = useRef();
    const lastNameRef = useRef();
    const phoneRef = useRef();
    const addressRef = useRef();
    const licenseRef = useRef();

    const validateForm = (data) => {
        const errors = {};

        if (!data.username.trim()) errors.username = 'Felhasználónév kötelező!';
        if (!data.email.trim()) errors.email = 'Email cím kötelező!';
        if (!data.password.trim()) errors.password = 'Jelszó megadása kötelező!';
        if (data.password !== data.confirmPassword) {
            errors.confirmPassword = 'A jelszavak nem egyeznek!';
        }
        if (!data.lastName.trim()) errors.lastName = 'Vezetéknév kötelező!';
        if (!data.firstName.trim()) errors.firstName = 'Keresztnév kötelező!';

        return errors;
    };

    const handleSubmit = async (e) => {
        e.preventDefault();

        const formData = {
            username: userNameRef.current.value,
            email: emailRef.current.value,
            password: passwordRef.current.value,
            confirmPassword: confirmPasswordRef.current.value,
            firstName: firstNameRef.current.value,
            lastName: lastNameRef.current.value,
            phone: phoneRef.current.value,
            address: addressRef.current.value,
            drivingLicence: licenseRef.current.value
        };

        const errors = validateForm(formData);
        if (Object.keys(errors).length > 0) {
            setValidationErrors(errors);
            return;
        }

        setValidationErrors({});
        // await register(formData);
    };

    return (
        <form onSubmit={handleSubmit}>
            <h2>Regisztráció</h2>

            <div>
                <label>Felhasználónév</label>
                <input type="text" ref={userNameRef} />
                {validationErrors.username && <span>{validationErrors.username}</span>}
            </div>

            <div>
                <label>Email cím</label>
                <input type="email" ref={emailRef} />
                {validationErrors.email && <span>{validationErrors.email}</span>}
            </div>

            <div>
                <label>Jelszó</label>
                <input type="password" ref={passwordRef} />
                {validationErrors.password && <span>{validationErrors.password}</span>}
            </div>

            <div>
                <label>Jelszó újra</label>
                <input type="password" ref={confirmPasswordRef} />
                {validationErrors.confirmPassword && <span>{validationErrors.confirmPassword}</span>}
            </div>

            <div>
                <label>Vezetéknév</label>
                <input type="text" ref={lastNameRef} />
                {validationErrors.lastName && <span>{validationErrors.lastName}</span>}
            </div>

            <div>
                <label>Keresztnév</label>
                <input type="text" ref={firstNameRef} />
                {validationErrors.firstName && <span>{validationErrors.firstName}</span>}
            </div>

            <div>
                <label>Lakcím</label>
                <input type="text" ref={addressRef} />
            </div>

            <div>
                <label>Telefonszám</label>
                <input type="tel" ref={phoneRef} />
            </div>

            <div>
                <label>Jogosítvány száma</label>
                <input type="text" ref={licenseRef} />
            </div>

            <button type="submit">Fiók létrehozása</button>
        </form>
    );
};

export default RegisterForm;