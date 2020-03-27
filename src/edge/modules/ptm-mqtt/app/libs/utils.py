import os

def get_env_variable(env_var_name):
    if env_var_name in os.environ:
        value = os.environ[env_var_name]
        print("{} = {}".format(env_var_name, value))
    else:
        error_msg = "ERROR: environment variable {} not found!".format(value)
        print(error_msg)
        raise Exception(error_msg)

    return value