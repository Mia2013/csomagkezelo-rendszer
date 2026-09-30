import { useRef, useState } from "react";
import {
  Box,
  Button,
  Container,
  FormControl,
  IconButton,
  InputAdornment,
  InputLabel,
  OutlinedInput,
  Paper,
  TextField,
  Typography,
} from "@mui/material";
import { Link } from "react-router";
import VisibilityOff from "@mui/icons-material/VisibilityOff";
import Visibility from "@mui/icons-material/Visibility";
import LoginIcon from "@mui/icons-material/Login";

import { useAuth } from "../provider/AuthProvider";
import ValidationCaption from "./ValidationCaption";

const LoginForm = () => {
  const { logIn } = useAuth();

  const [showPassword, setShowPassword] = useState(false);
  const [validationErrors, setValidationErrors] = useState({});

  const emailRef = useRef();
  const passwordRef = useRef();

  const handleClickShowPassword = () => setShowPassword((show) => !show);

  const validateForm = (data) => {
    const errors = {};

    if (!data.email.trim()) errors.email = "Email cím kötelező!";
    if (!data.password.trim()) errors.password = "Jelszó megadása kötelező!";

    return errors;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    const formData = {
      email: emailRef.current.value,
      password: passwordRef.current.value,
    };

    const errors = validateForm(formData);
    if (Object.keys(errors).length > 0) {
      setValidationErrors(errors);
      return;
    }

    setValidationErrors({});
    await logIn(formData.email, formData.password);
  };

  return (
    <Container maxWidth="sm">
      <Paper sx={{ p: 4, my: 4 }}>
        <Typography variant="h5" component="h1" gutterBottom align="center">
          Belépés
        </Typography>

        <Box
          component="form"
          onSubmit={handleSubmit}
          sx={{ display: "flex", flexDirection: "column", gap: 2, mt: 2 }}
        >
          <TextField
            fullWidth
            label="Email cím*"
            type="email"
            autoComplete="email"
            inputRef={emailRef}
            error={!!validationErrors.email}
            helperText={validationErrors.email}
          />

          <FormControl
            variant="outlined"
            fullWidth
            error={!!validationErrors.password}
          >
            <InputLabel>Jelszó*</InputLabel>
            <OutlinedInput
              inputRef={passwordRef}
              type={showPassword ? "text" : "password"}
              autoComplete="current-password"
              label="Jelszó*"
              endAdornment={
                <InputAdornment position="end">
                  <IconButton onClick={handleClickShowPassword} edge="end">
                    {showPassword ? <VisibilityOff /> : <Visibility />}
                  </IconButton>
                </InputAdornment>
              }
            />
            {validationErrors.password && (
              <ValidationCaption message={validationErrors.password} />
            )}
          </FormControl>

          <Button
            type="submit"
            variant="contained"
            startIcon={<LoginIcon />}
            sx={{ py: 1.5, mt: 1 }}
          >
            Belépés
          </Button>

          <Typography variant="body2" align="center">
            Még nincs fiókod?{" "}
            <Link to="/register">Regisztrálj!</Link>
          </Typography>
        </Box>
      </Paper>
    </Container>
  );
};

export default LoginForm;